using System.Net;
using ClaimsModule.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace ClaimsModule.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public sealed class AuthenticationTests(ApiFixture fixture)
{
    [Fact]
    public async Task API_AUTH_Missing_token_gets_401()
    {
        var response = await fixture.Factory.CreateClient().GetAsync("/test/probe/me");

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Unauthorized");
    }

    [Fact]
    public async Task API_AUTH_Tampered_token_gets_401()
    {
        var client = fixture.Factory.CreateClient();
        var token = (await client.SignInAsync("handler.alex")).AccessToken;
        var tampered = token[..^4] + (token.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");

        var response = await client.WithBearer(tampered).GetAsync("/test/probe/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task API_AUTH_Token_signed_with_another_key_gets_401()
    {
        await using var otherIssuer = fixture.Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Auth:SigningKey", "a-different-signing-key-that-is-long-enough"));
        var foreignToken = (await otherIssuer.CreateClient().SignInAsync("handler.alex")).AccessToken;

        var response = await fixture.Factory.CreateClient().WithBearer(foreignToken).GetAsync("/test/probe/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task API_AUTH_Expired_token_gets_401()
    {
        // Issue a token 9 hours in the past: its 8-hour lifetime ended an hour ago.
        var past = new FakeTimeProvider(DateTimeOffset.UtcNow.AddHours(-9));
        await using var pastIssuer = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(past)));
        var expiredToken = (await pastIssuer.CreateClient().SignInAsync("handler.alex")).AccessToken;

        var response = await fixture.Factory.CreateClient().WithBearer(expiredToken).GetAsync("/test/probe/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task API_AUTH_Anonymous_endpoints_do_not_need_a_token()
    {
        var client = fixture.Factory.CreateClient();

        (await client.GetAsync("/api/auth/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/health/live")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
