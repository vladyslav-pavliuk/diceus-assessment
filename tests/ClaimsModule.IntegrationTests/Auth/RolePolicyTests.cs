using System.Net;
using ClaimsModule.IntegrationTests.Fixtures;

namespace ClaimsModule.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public sealed class RolePolicyTests(ApiFixture fixture)
{
    [Theory]
    [InlineData("handler.alex", "handler", HttpStatusCode.OK)]
    [InlineData("handler.alex", "supervisor", HttpStatusCode.Forbidden)]
    [InlineData("handler.alex", "manager", HttpStatusCode.Forbidden)]
    [InlineData("supervisor.casey", "handler", HttpStatusCode.OK)]
    [InlineData("supervisor.casey", "supervisor", HttpStatusCode.OK)]
    [InlineData("supervisor.casey", "manager", HttpStatusCode.Forbidden)]
    [InlineData("manager.emery", "handler", HttpStatusCode.OK)]
    [InlineData("manager.emery", "supervisor", HttpStatusCode.OK)]
    [InlineData("manager.emery", "manager", HttpStatusCode.OK)]
    public async Task SEC_01_Role_policies_are_hierarchical(string username, string policyEndpoint, HttpStatusCode expected)
    {
        var client = fixture.Factory.CreateClient();
        var token = (await client.SignInAsync(username)).AccessToken;

        var response = await client.WithBearer(token).GetAsync($"/test/probe/{policyEndpoint}");

        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task SEC_02_Forbidden_role_gets_403_problem()
    {
        var client = fixture.Factory.CreateClient();
        var token = (await client.SignInAsync("handler.blake")).AccessToken;

        var response = await client.WithBearer(token).GetAsync("/test/probe/supervisor");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden");
    }
}
