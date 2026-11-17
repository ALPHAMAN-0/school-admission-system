using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SchoolAdmission.Api.Tests.Infrastructure;

public static class ApiClientExtensions
{
    private static long _studentCounter = Random.Shared.NextInt64(1_000_000_000, 9_000_000_000);

    /// <summary>A new, unique, valid 17-digit birth registration number.</summary>
    public static string NewStudentId() => "2015" + Interlocked.Increment(ref _studentCounter).ToString("D13");

    public static async Task<string> CreateClassAsync(this HttpClient client, int totalSeats, string? name = null)
    {
        var response = await client.PostAsJsonAsync("/classes", new
        {
            name = name ?? $"Class {Guid.NewGuid():N}",
            totalSeats,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        return body.GetProperty("id").GetString()!;
    }

    public static Task<HttpResponseMessage> ApplyAsync(this HttpClient client, string classId, string studentId) =>
        client.SendWithStudentAsync(HttpMethod.Post, $"/classes/{classId}/holds", studentId);

    public static async Task<string> ApplyAndGetHoldIdAsync(this HttpClient client, string classId, string studentId)
    {
        var response = await client.ApplyAsync(classId, studentId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadJsonAsync()).GetProperty("holdId").GetString()!;
    }

    public static Task<HttpResponseMessage> ConfirmAsync(this HttpClient client, string holdId, string studentId) =>
        client.SendWithStudentAsync(HttpMethod.Post, $"/holds/{holdId}/confirm", studentId);

    public static Task<HttpResponseMessage> CancelAsync(this HttpClient client, string holdId, string studentId) =>
        client.SendWithStudentAsync(HttpMethod.Delete, $"/holds/{holdId}", studentId);

    public static async Task<JsonElement> GetClassAsync(this HttpClient client, string classId)
    {
        var response = await client.GetAsync($"/classes/{classId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    public static async Task<JsonElement> GetHoldAsync(this HttpClient client, string holdId)
    {
        var response = await client.GetAsync($"/holds/{holdId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    public static async Task<HttpResponseMessage> SendWithStudentAsync(this HttpClient client, HttpMethod method, string url, string? studentId)
    {
        using var request = new HttpRequestMessage(method, url);
        if (studentId is not null)
        {
            // Without validation so the tests can send deliberately malformed IDs.
            request.Headers.TryAddWithoutValidation("X-Student-Id", studentId);
        }

        return await client.SendAsync(request);
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    /// <summary>The stable error code from a problem-details response.</summary>
    public static async Task<string?> ReadErrorCodeAsync(this HttpResponseMessage response) =>
        (await response.ReadJsonAsync()).GetProperty("code").GetString();

    public static int Int(this JsonElement element, string property) => element.GetProperty(property).GetInt32();

    public static string? Str(this JsonElement element, string property) => element.GetProperty(property).GetString();
}
