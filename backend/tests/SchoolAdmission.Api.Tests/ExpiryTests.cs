using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Api.Services;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

/// <summary>
/// Rule 2: when HOLD_DURATION_SECONDS runs out the seat returns by itself, with nobody calling
/// any API. The tests move a manual clock instead of sleeping.
/// </summary>
public sealed class ExpiryTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static readonly TimeSpan PastHoldDuration = TimeSpan.FromSeconds(601);

    private HttpClient Client => fixture.Client;

    [Fact]
    public async Task ExpiredHold_FreesItsSeat_WithoutAnyApiCall()
    {
        var classId = await Client.CreateClassAsync(totalSeats: 1);
        var holdId = await Client.ApplyAndGetHoldIdAsync(classId, ApiClientExtensions.NewStudentId());
        Assert.Equal(0, (await Client.GetClassAsync(classId)).Int("availableSeats"));

        fixture.Time.Advance(PastHoldDuration);

        var seats = await Client.GetClassAsync(classId);
        Assert.Equal(0, seats.Int("heldSeats"));
        Assert.Equal(1, seats.Int("availableSeats"));
        Assert.Equal("EXPIRED", (await Client.GetHoldAsync(holdId)).Str("status"));

        // Someone else can now take the seat.
        Assert.Equal(HttpStatusCode.Created, (await Client.ApplyAsync(classId, ApiClientExtensions.NewStudentId())).StatusCode);
    }

    [Fact]
    public async Task Confirm_Returns409_AfterHoldExpired_AndDoesNotCountSeat()
    {
        var classId = await Client.CreateClassAsync(totalSeats: 3);
        var studentId = ApiClientExtensions.NewStudentId();
        var holdId = await Client.ApplyAndGetHoldIdAsync(classId, studentId);

        fixture.Time.Advance(PastHoldDuration);
        var confirm = await Client.ConfirmAsync(holdId, studentId);

        Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
        Assert.Equal("HOLD_EXPIRED", await confirm.ReadErrorCodeAsync());
        var seats = await Client.GetClassAsync(classId);
        Assert.Equal(0, seats.Int("confirmedSeats"));
        Assert.Equal(3, seats.Int("availableSeats"));
    }

    [Fact]
    public async Task Confirm_AtTheVeryEndOfTheHoldDuration_Succeeds()
    {
        var classId = await Client.CreateClassAsync(totalSeats: 3);
        var studentId = ApiClientExtensions.NewStudentId();
        var holdId = await Client.ApplyAndGetHoldIdAsync(classId, studentId);

        // The full HOLD_DURATION_SECONDS is honoured, to the millisecond.
        fixture.Time.Advance(TimeSpan.FromSeconds(600) - TimeSpan.FromMilliseconds(1));

        Assert.Equal(HttpStatusCode.OK, (await Client.ConfirmAsync(holdId, studentId)).StatusCode);
        fixture.Time.Advance(TimeSpan.FromHours(1));
        Assert.Equal("CONFIRMED", (await Client.GetHoldAsync(holdId)).Str("status"));
        Assert.Equal(1, (await Client.GetClassAsync(classId)).Int("confirmedSeats"));
    }

    [Fact]
    public async Task Student_CanApplyAgain_AfterTheirHoldExpired()
    {
        var classId = await Client.CreateClassAsync(totalSeats: 2);
        var studentId = ApiClientExtensions.NewStudentId();
        var oldHoldId = await Client.ApplyAndGetHoldIdAsync(classId, studentId);

        fixture.Time.Advance(PastHoldDuration);
        var reapply = await Client.ApplyAsync(classId, studentId);

        Assert.Equal(HttpStatusCode.Created, reapply.StatusCode);
        Assert.NotEqual(oldHoldId, (await reapply.ReadJsonAsync()).Str("holdId"));
        Assert.Equal(1, (await Client.GetClassAsync(classId)).Int("heldSeats"));
    }

    [Fact]
    public async Task Cancel_AfterHoldExpired_Returns204_AndKeepsStatusExpired()
    {
        var classId = await Client.CreateClassAsync(totalSeats: 2);
        var studentId = ApiClientExtensions.NewStudentId();
        var holdId = await Client.ApplyAndGetHoldIdAsync(classId, studentId);

        fixture.Time.Advance(PastHoldDuration);
        var cancel = await Client.CancelAsync(holdId, studentId);

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal("EXPIRED", (await Client.GetHoldAsync(holdId)).Str("status"));
        Assert.Equal(2, (await Client.GetClassAsync(classId)).Int("availableSeats"));
    }

    [Fact]
    public async Task Sweeper_RewritesStaleHeldRowsToExpired()
    {
        var classId = await Client.CreateClassAsync(totalSeats: 2);
        await Client.ApplyAndGetHoldIdAsync(classId, ApiClientExtensions.NewStudentId());
        fixture.Time.Advance(PastHoldDuration);

        var sweeper = fixture.Factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<HoldExpirySweeper>()
            .Single();
        await sweeper.SweepOnceAsync(CancellationToken.None);

        Assert.Equal(0, await fixture.Database.ScalarAsync(
            $"SELECT count(*) FROM holds WHERE class_id = '{classId}' AND status = 'HELD'"));
        Assert.Equal(1, await fixture.Database.ScalarAsync(
            $"SELECT count(*) FROM holds WHERE class_id = '{classId}' AND status = 'EXPIRED'"));
    }
}
