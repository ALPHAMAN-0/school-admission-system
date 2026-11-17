using System.Net;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

/// <summary>
/// Rule 1: held + confirmed never exceeds totalSeats, however many requests arrive at once.
/// Every test fires all requests at the same moment (released by one gate) and then checks both
/// the HTTP results and the database itself.
/// </summary>
public sealed class ConcurrencyTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task ManyConcurrentApplications_NeverHoldMoreSeatsThanTotalSeats()
    {
        // Far fewer seats than requests in flight at once (the pool allows 25 concurrent
        // transactions): without the class row lock this test reliably over-books.
        const int totalSeats = 10;
        const int applicants = 200;
        var classId = await fixture.Client.CreateClassAsync(totalSeats);

        var responses = await FireAtOnceAsync(applicants, _ =>
            fixture.Client.ApplyAsync(classId, ApiClientExtensions.NewStudentId()));

        Assert.Equal(totalSeats, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(applicants - totalSeats, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var rejected in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
        {
            Assert.Equal("NO_SEATS_AVAILABLE", await rejected.ReadErrorCodeAsync());
        }

        var seats = await fixture.Client.GetClassAsync(classId);
        Assert.Equal(totalSeats, seats.Int("heldSeats"));
        Assert.Equal(0, seats.Int("availableSeats"));
        Assert.Equal(totalSeats, await CountActiveHoldsInDatabaseAsync(fixture.Database, classId));
    }

    [Fact]
    public async Task SameStudentApplyingManyTimesAtOnce_GetsExactlyOneHold()
    {
        var classId = await fixture.Client.CreateClassAsync(totalSeats: 10);
        var studentId = ApiClientExtensions.NewStudentId();

        var responses = await FireAtOnceAsync(20, _ => fixture.Client.ApplyAsync(classId, studentId));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(19, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, (await fixture.Client.GetClassAsync(classId)).Int("heldSeats"));
    }

    [Fact]
    public async Task ConcurrentConfirmsOfOneHold_IncreaseConfirmedSeatsOnlyOnce()
    {
        var classId = await fixture.Client.CreateClassAsync(totalSeats: 5);
        var studentId = ApiClientExtensions.NewStudentId();
        var holdId = await fixture.Client.ApplyAndGetHoldIdAsync(classId, studentId);

        var responses = await FireAtOnceAsync(15, _ => fixture.Client.ConfirmAsync(holdId, studentId));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var seats = await fixture.Client.GetClassAsync(classId);
        Assert.Equal(1, seats.Int("confirmedSeats"));
        Assert.Equal(0, seats.Int("heldSeats"));
    }

    [Fact]
    public async Task TwoBackendInstancesSharingOneDatabase_NeverHoldMoreSeatsThanTotalSeats()
    {
        // Bonus rule: two backend processes on different ports, one database. The second factory
        // is a separate app instance; a different Application Name gives it its own connection
        // pool, as a separate process would have. Only PostgreSQL is shared.
        var secondConnectionString = new Npgsql.NpgsqlConnectionStringBuilder(fixture.Database.ConnectionString)
        {
            ApplicationName = "admission-api-instance-2",
        }.ConnectionString;
        await using var secondInstance = new AdmissionApiFactory(secondConnectionString);
        using var secondClient = secondInstance.CreateClient();

        const int totalSeats = 8;
        var classId = await fixture.Client.CreateClassAsync(totalSeats);

        var responses = await FireAtOnceAsync(120, i =>
            (i % 2 == 0 ? fixture.Client : secondClient).ApplyAsync(classId, ApiClientExtensions.NewStudentId()));

        Assert.Equal(totalSeats, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses, r => Assert.True(
            r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            $"Unexpected status {(int)r.StatusCode}"));
        Assert.Equal(totalSeats, await CountActiveHoldsInDatabaseAsync(fixture.Database, classId));
    }

    private static async Task<HttpResponseMessage[]> FireAtOnceAsync(int count, Func<int, Task<HttpResponseMessage>> send)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable.Range(0, count)
            .Select(i => Task.Run(async () =>
            {
                await gate.Task;
                return await send(i);
            }))
            .ToArray();

        gate.SetResult();
        return await Task.WhenAll(requests);
    }

    private static Task<long> CountActiveHoldsInDatabaseAsync(TestDatabase database, string classId) =>
        database.ScalarAsync(
            $"SELECT count(*) FROM holds WHERE class_id = '{classId}' AND status IN ('HELD', 'CONFIRMED')");
}
