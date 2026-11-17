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
    [InlineData("""{"name":"Class X"}""")]
    [InlineData("""{"name":"Class\u0000X","totalSeats":5}""")]
    [InlineData("""{"name":"Class\u0001X","totalSeats":5}""")]
    [InlineData("""{"name":"Class \ud800 X","totalSeats":5}""")]
    [InlineData("""{"name":null,"totalSeats":5}""")]
    [InlineData("""{"name":123,"totalSeats":5}""")]
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
    public async Task CreateClass_AllowsTheSameNameTwice_AsSeparateClasses()
    {
        // The brief does not require unique names; repeated test runs must keep getting 201.
        var first = await fixture.Client.CreateClassAsync(totalSeats: 10, "Class 6 - Morning Shift");
        var second = await fixture.Client.CreateClassAsync(totalSeats: 5, "Class 6 - Morning Shift");

        Assert.NotEqual(first, second);
        Assert.Equal(10, (await fixture.Client.GetClassAsync(first)).Int("totalSeats"));
        Assert.Equal(5, (await fixture.Client.GetClassAsync(second)).Int("totalSeats"));
    }

    [Fact]
    public async Task CreateClass_AcceptsLargeSeatCounts_AndTidiesTheName()
    {
        var response = await fixture.Client.PostAsJsonAsync("/classes", new { name = "  Class 10 -   Day Shift ", totalSeats = 250_000 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("Class 10 - Day Shift", body.Str("name"));
        Assert.Equal(250_000, body.Int("totalSeats"));
    }

    [Theory]
    [InlineData("c_doesnotexist")]
    [InlineData("0123456789012345678901234567890123456789-way-too-long")]
    [InlineData("%E0%A6%95")]
    [InlineData("c_1%27%20OR%201%3D1")]
    public async Task GetClass_Returns404_ForUnknownId(string classId)
    {
        var response = await fixture.Client.GetAsync($"/classes/{classId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("CLASS_NOT_FOUND", await response.ReadErrorCodeAsync());
    }
}
