using System.Net;
using System.Net.Http.Json;
using System.Text;
using ClaimsModule.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;

namespace ClaimsModule.IntegrationTests.Errors;

[Collection(ApiCollection.Name)]
public sealed class ErrorResponseTests(ApiFixture fixture)
{
    [Fact]
    public async Task API_ERR_422_body_matches_FRS_10_4_exactly()
    {
        // Goes through the real pipeline: model binding → MediatR → ValidationBehavior → middleware.
        var response = await fixture.Factory.CreateClient().PostAsJsonAsync("/api/auth/dev-token", new { username = "" });

        var body = await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ValidationError");
        body.PropertyNames().ShouldBe(["type", "title", "status", "errors"], ignoreOrder: true);
        body.GetProperty("title").GetString().ShouldBe("One or more validation errors occurred.");
        body.GetProperty("errors").PropertyNames().ShouldBe(["Username"]);
        body.GetProperty("errors").GetProperty("Username").EnumerateArray().Select(message => message.GetString())
            .ShouldBe(["Username is required."]);
    }

    [Fact]
    public async Task API_ERR_Business_rule_violation_gets_422_with_the_same_shape()
    {
        var response = await fixture.Factory.CreateClient().GetAsync("/test/probe/throw/business-rule");

        var body = await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ValidationError");
        body.PropertyNames().ShouldBe(["type", "title", "status", "errors"], ignoreOrder: true);
        body.GetProperty("errors").GetProperty("ApprovedByUserId")[0].GetString().ShouldBe("Self-approval is not permitted.");
    }

    [Fact]
    public async Task API_ERR_Unbindable_body_gets_422_with_the_same_shape()
    {
        var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        var response = await fixture.Factory.CreateClient().PostAsync("/api/auth/dev-token", content);

        var body = await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ValidationError");
        body.PropertyNames().ShouldBe(["type", "title", "status", "errors"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("not-found", HttpStatusCode.NotFound, "NotFound")]
    [InlineData("forbidden", HttpStatusCode.Forbidden, "Forbidden")]
    [InlineData("concurrency", HttpStatusCode.Conflict, "Conflict")]
    public async Task API_ERR_Exceptions_map_to_status_codes(string kind, HttpStatusCode status, string type)
    {
        var response = await fixture.Factory.CreateClient().GetAsync($"/test/probe/throw/{kind}");

        var body = await response.ShouldBeProblemAsync(status, type);
        body.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task API_ERR_Unknown_route_gets_404_problem_for_a_signed_in_caller()
    {
        var client = fixture.Factory.CreateClient();
        var token = (await client.SignInAsync("handler.alex")).AccessToken;

        var response = await client.WithBearer(token).GetAsync("/api/does-not-exist");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound");
    }

    [Fact]
    public async Task API_AUTH_Unknown_route_gets_401_for_an_anonymous_caller()
    {
        // The fallback policy also covers requests that match no endpoint, so anonymous callers
        // cannot probe which routes exist.
        var response = await fixture.Factory.CreateClient().GetAsync("/api/does-not-exist");

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Unauthorized");
    }

    [Fact]
    public async Task API_ERR_Unhandled_exception_gets_500_without_internal_details_outside_development()
    {
        await using var production = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Auth:SigningKey", "production-like-signing-key-for-this-test-only");

            // Outside Development documents must go to Azure Blob Storage, or the host refuses to start (D-42). The client is
            // created on first use, which this test never reaches.
            builder.UseSetting("Storage:AzureBlob:ServiceUri", "https://claimsdocs.blob.core.windows.net");
        });

        var response = await production.CreateClient().GetAsync("/test/probe/throw/unexpected");

        var body = await response.ShouldBeProblemAsync(HttpStatusCode.InternalServerError, "ServerError");
        body.GetProperty("title").GetString().ShouldBe("An unexpected error occurred.");
        body.TryGetProperty("detail", out _).ShouldBeFalse();
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("Sensitive internal detail");
    }
}
