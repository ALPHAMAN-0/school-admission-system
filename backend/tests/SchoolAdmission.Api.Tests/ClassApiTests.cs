using System.Net;
using System.Net.Http.Json;
using System.Text;
using SchoolAdmission.Api.Tests.Infrastructure;

namespace SchoolAdmission.Api.Tests;

public sealed class ClassApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task CreateClass_Returns201_WithContractFields()
    {
        var response = await fixture.Client.PostAsJsonAsync("/classes", new { name = "Class 6 - Morning Shift", totalSeats = 120 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.StartsWith("c_", body.Str("id"));
        Assert.Equal("Class 6 - Morning Shift", body.Str("name"));
        Assert.Equal(120, body.Int("totalSeats"));
        Assert.Equal($"/classes/{body.Str("id")}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ListAndGetClass_ReturnSeatCounts()
    {
        var classId = await fixture.Client.CreateClassAsync(totalSeats: 3);
        await fixture.Client.ApplyAndGetHoldIdAsync(classId, ApiClientExtensions.NewStudentId());
        await fixture.Client.ApplyAndGetHoldIdAsync(classId, ApiClientExtensions.NewStudentId());

        var single = await fixture.Client.GetClassAsync(classId);
        Assert.Equal(3, single.Int("totalSeats"));
        Assert.Equal(2, single.Int("heldSeats"));
        Assert.Equal(0, single.Int("confirmedSeats"));
        Assert.Equal(1, single.Int("availableSeats"));

        var list = await (await fixture.Client.GetAsync("/classes")).ReadJsonAsync();
        var fromList = list.EnumerateArray().Single(c => c.Str("id") == classId);
        Assert.Equal(2, fromList.Int("heldSeats"));
        Assert.Equal(1, fromList.Int("availableSeats"));
    }

    [Theory]
    [InlineData("""{"name":"","totalSeats":10}""")]
    [InlineData("""{"name":"   ","totalSeats":10}""")]
    [InlineData("""{"totalSeats":10}""")]
    [InlineData("""{"name":"Class X","totalSeats":0}""")]
    [InlineData("""{"name":"Class X","totalSeats":-5}""")]
    [InlineData("""{"name":"Class X","totalSeats":10001}""")]
    [InlineData("""{"name":"Class X"}""")]
    [InlineData("""{"name":"Class X","totalSeats":"many"}""")]
    [InlineData("""{"name":"Class X","totalSeats":12.5}""")]
    [InlineData("""{"name":"Class X","totalSeats":99999999999}""")]
    [InlineData("""not json at all""")]
    [InlineData("""[]""")]
    [InlineData("")]
    public async Task CreateClass_Returns400_ForInvalidInput(string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await fixture.Client.PostAsync("/classes", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateClass_Returns409_WhenNameAlreadyExists_IgnoringCaseAndSpacing()
    {
        var name = $"Class 7 - Day Shift {Guid.NewGuid():N}";
        await fixture.Client.CreateClassAsync(totalSeats: 10, name);

        var response = await fixture.Client.PostAsJsonAsync("/classes", new { name = "  " + name.ToUpperInvariant().Replace(" ", "  ") + " ", totalSeats = 5 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CLASS_NAME_TAKEN", await response.ReadErrorCodeAsync());
    }

    [Theory]
    [InlineData("c_doesnotexist")]
    [InlineData("0123456789012345678901234567890123456789-way-too-long")]
    public async Task GetClass_Returns404_ForUnknownId(string classId)
    {
        var response = await fixture.Client.GetAsync($"/classes/{classId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("CLASS_NOT_FOUND", await response.ReadErrorCodeAsync());
    }
}
