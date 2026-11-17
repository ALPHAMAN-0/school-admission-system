using System.Net;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

public sealed class HoldApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private HttpClient Client => fixture.Client;

    [Fact]
    public async Task Apply_Returns201_WithContractFields_AndHoldsASeat()
    {
        var classId = await Client.CreateClassAsync(totalSeats: 5);
        var studentId = ApiClientExtensions.NewStudentId();

        var response = await Client.ApplyAsync(classId, studentId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var hold = await response.ReadJsonAsync();
        Assert.StartsWith("h_", hold.Str("holdId"));
        Assert.Equal(classId, hold.Str("classId"));
        Assert.Equal(studentId, hold.Str("studentId"));
        Assert.Equal("HELD", hold.Str("status"));
        // Never shorter than HOLD_DURATION_SECONDS; rounded up to a whole second.
        var expiresAt = DateTimeOffset.Parse(hold.Str("expiresAt")!);
        var earliest = fixture.Time.GetUtcNow().AddSeconds(600);
        Assert.InRange(expiresAt, earliest, earliest.AddSeconds(1));
        Assert.EndsWith("Z", hold.Str("expiresAt"));
        Assert.Equal(1, (await Client.GetClassAsync(classId)).Int("heldSeats"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("2015269123456789A")]
    [InlineData("201526912345678901")]
    [InlineData("২০১৫২৬৯১২৩৪৫৬৭৮৯০")]
    public async Task StudentEndpoints_Return400_WhenStudentIdIsMissingOrInvalid(string? studentId)
    {
        var classId = await Client.CreateClassAsync(totalSeats: 5);

        var apply = await Client.ApplyAsync(classId, studentId!);
        var mine = await Client.SendWithStudentAsync(HttpMethod.Get, "/me/holds", studentId);

        Assert.Equal(HttpStatusCode.BadRequest, apply.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, mine.StatusCode);
        Assert.Contains(await apply.ReadErrorCodeAsync(), new[] { "MISSING_STUDENT_ID", "INVALID_STUDENT_ID" });
    }

    [Fact]
    public async Task Apply_Returns409_WhenStudentAlreadyHasActiveHoldInClass()
    {
        var classId = await Client.CreateClassAsync(totalSeats: 5);
        var studentId = ApiClientExtensions.NewStudentId();
        var firstHoldId = await Client.ApplyAndGetHoldIdAsync(classId, studentId);

        var second = await Client.ApplyAsync(classId, studentId);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await second.ReadJsonAsync();
        Assert.Equal("ALREADY_APPLIED", problem.Str("code"));
        Assert.Equal(firstHoldId, problem.Str("holdId"));
    }

    [Fact]
    public async Task Apply_Returns409_WhenStudentIsAlreadyAdmittedToClass()
    {
        var classId = await Client.CreateClassAsync(totalSeats: 5);
        var studentId = ApiClientExtensions.NewStudentId();
        var holdId = await Client.ApplyAndGetHoldIdAsync(classId, studentId);
        Assert.Equal(HttpStatusCode.OK, (await Client.ConfirmAsync(holdId, studentId)).StatusCode);

        var again = await Client.ApplyAsync(classId, studentId);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("ALREADY_APPLIED", await again.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task SameStudent_CanApplyToDifferentClasses()
    {
        var studentId = ApiClientExtensions.NewStudentId();
        var morning = await Client.CreateClassAsync(totalSeats: 5);
        var day = await Client.CreateClassAsync(totalSeats: 5);

        Assert.Equal(HttpStatusCode.Created, (await Client.ApplyAsync(morning, studentId)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Client.ApplyAsync(day, studentId)).StatusCode);
    }

    [Fact]
    public async Task Apply_Returns409_WhenClassIsFull()
    {
        var classId = await Client.CreateClassAsync(totalSeats: 1);
        await Client.ApplyAndGetHoldIdAsync(classId, ApiClientExtensions.NewStudentId());

        var response = await Client.ApplyAsync(classId, ApiClientExtensions.NewStudentId());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("NO_SEATS_AVAILABLE", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Apply_Returns404_ForUnknownClass()
    {
        var response = await Client.ApplyAsync("c_doesnotexist", ApiClientExtensions.NewStudentId());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("CLASS_NOT_FOUND", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Confirm_MovesSeatFromHeldToConfirmed_AndIsIdempotent()
    {
        var classId = await Client.CreateClassAsync(totalSeats: 5);
        var studentId = ApiClientExtensions.NewStudentId();
        var holdId = await Client.ApplyAndGetHoldIdAsync(classId, studentId);

        var first = await Client.ConfirmAsync(holdId, studentId);
        var second = await Client.ConfirmAsync(holdId, studentId);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var body = await first.ReadJsonAsync();
        Assert.Equal(holdId, body.Str("holdId"));
        Assert.Equal("CONFIRMED", body.Str("status"));

        var seats = await Client.GetClassAsync(classId);
        Assert.Equal(1, seats.Int("confirmedSeats"));
        Assert.Equal(0, seats.Int("heldSeats"));
        Assert.Equal(4, seats.Int("availableSeats"));
        Assert.Equal("CONFIRMED", (await Client.GetHoldAsync(holdId)).Str("status"));
    }

    [Fact]
    public async Task ConfirmAndCancel_Return403_ForAnotherStudentsHold()
    {
        var classId = await Client.CreateClassAsync(totalSeats: 5);
        var holdId = await Client.ApplyAndGetHoldIdAsync(classId, ApiClientExtensions.NewStudentId());
        var stranger = ApiClientExtensions.NewStudentId();

        var confirm = await Client.ConfirmAsync(holdId, stranger);
        var cancel = await Client.CancelAsync(holdId, stranger);

        Assert.Equal(HttpStatusCode.Forbidden, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
        Assert.Equal("NOT_HOLD_OWNER", await confirm.ReadErrorCodeAsync());
        Assert.Equal("HELD", (await Client.GetHoldAsync(holdId)).Str("status"));
    }

    [Fact]
    public async Task Cancel_Returns204_ReleasesSeat_AndIsIdempotent()
    {
        var classId = await Client.CreateClassAsync(totalSeats: 1);
        var studentId = ApiClientExtensions.NewStudentId();
        var holdId = await Client.ApplyAndGetHoldIdAsync(classId, studentId);

        var first = await Client.CancelAsync(holdId, studentId);
        var second = await Client.CancelAsync(holdId, studentId);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal("CANCELLED", (await Client.GetHoldAsync(holdId)).Str("status"));
        Assert.Equal(1, (await Client.GetClassAsync(classId)).Int("availableSeats"));

        // The freed seat can be taken by someone else, and the student may apply again.
        Assert.Equal(HttpStatusCode.Created, (await Client.ApplyAsync(classId, studentId)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.ConfirmAsync(holdId, studentId)).StatusCode);
    }

    [Fact]
    public async Task Cancel_Returns409_WhenAdmissionIsAlreadyConfirmed()
    {
        var classId = await Client.CreateClassAsync(totalSeats: 5);
        var studentId = ApiClientExtensions.NewStudentId();
        var holdId = await Client.ApplyAndGetHoldIdAsync(classId, studentId);
        await Client.ConfirmAsync(holdId, studentId);

        var cancel = await Client.CancelAsync(holdId, studentId);

        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Equal("HOLD_ALREADY_CONFIRMED", await cancel.ReadErrorCodeAsync());
        Assert.Equal(1, (await Client.GetClassAsync(classId)).Int("confirmedSeats"));
    }

    [Theory]
    [InlineData("h_doesnotexist")]
    [InlineData("h_%E2%82%AC")]
    [InlineData("h_1%27%3B%20DROP%20TABLE%20holds")]
    public async Task HoldEndpoints_Return404_ForUnknownOrMalformedHoldId(string holdId)
    {
        var studentId = ApiClientExtensions.NewStudentId();

        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/holds/{holdId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.ConfirmAsync(holdId, studentId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.CancelAsync(holdId, studentId)).StatusCode);
    }

    [Fact]
    public async Task MyHolds_ListsOnlyThisStudentsApplications_WithClassNames()
    {
        var studentId = ApiClientExtensions.NewStudentId();
        var name = $"Class 9 - Morning {Guid.NewGuid():N}";
        var classId = await Client.CreateClassAsync(totalSeats: 5, name);
        var holdId = await Client.ApplyAndGetHoldIdAsync(classId, studentId);
        await Client.ApplyAndGetHoldIdAsync(classId, ApiClientExtensions.NewStudentId());

        var response = await Client.SendWithStudentAsync(HttpMethod.Get, "/me/holds", studentId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.Single((await response.ReadJsonAsync()).EnumerateArray());
        Assert.Equal(holdId, item.Str("holdId"));
        Assert.Equal(classId, item.Str("classId"));
        Assert.Equal(name, item.Str("className"));
        Assert.Equal("HELD", item.Str("status"));
        Assert.False(string.IsNullOrEmpty(item.Str("expiresAt")));
    }
}
