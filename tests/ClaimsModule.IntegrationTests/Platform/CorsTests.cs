using ClaimsModule.IntegrationTests.Fixtures;

namespace ClaimsModule.IntegrationTests.Platform;

[Collection(ApiCollection.Name)]
public sealed class CorsTests(ApiFixture fixture)
{
    private const string SpaOrigin = "http://localhost:4200";

    [Fact]
    public async Task OPS_02_Preflight_from_the_SPA_origin_is_allowed()
    {
        var response = await fixture.Factory.CreateClient().SendAsync(Preflight(SpaOrigin));

        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldHaveSingleItem().ShouldBe(SpaOrigin);
    }

    [Fact]
    public async Task OPS_02_Preflight_from_another_origin_is_not_allowed()
    {
        var response = await fixture.Factory.CreateClient().SendAsync(Preflight("https://evil.example"));

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    public async Task OPS_02_Correlation_and_location_headers_are_exposed_to_the_SPA()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/users");
        request.Headers.Add("Origin", SpaOrigin);

        var response = await fixture.Factory.CreateClient().SendAsync(request);

        var exposed = response.Headers.GetValues("Access-Control-Expose-Headers").ShouldHaveSingleItem();
        exposed.ShouldContain("X-Correlation-Id");
        exposed.ShouldContain("Location");
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/dev-token");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,idempotency-key");
        return request;
    }
}
