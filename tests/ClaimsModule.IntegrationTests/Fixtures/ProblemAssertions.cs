using System.Net;
using System.Text.Json;

namespace ClaimsModule.IntegrationTests.Fixtures;

internal static class ProblemAssertions
{
    /// <summary>Asserts the status code and problem+json content type, and returns the parsed body.</summary>
    public static async Task<JsonElement> ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string type)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("type").GetString().ShouldBe(type);
        body.GetProperty("status").GetInt32().ShouldBe((int)status);
        body.TryGetProperty("traceId", out _).ShouldBeFalse();
        return body;
    }

    public static IReadOnlyList<string> PropertyNames(this JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).ToList();
}
