using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaimsModule.API.Contracts.Auth;

namespace ClaimsModule.IntegrationTests.Fixtures;

internal static class TestAuth
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<DevTokenResponse> SignInAsync(this HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/auth/dev-token", new { username });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DevTokenResponse>(Json))!;
    }

    public static HttpClient WithBearer(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
