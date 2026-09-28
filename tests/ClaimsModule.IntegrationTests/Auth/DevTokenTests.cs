using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Users;
using ClaimsModule.Domain.Users;
using ClaimsModule.IntegrationTests.Fixtures;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ClaimsModule.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public sealed class DevTokenTests(ApiFixture fixture)
{
    [Fact]
    public async Task API_28_Dev_token_is_a_valid_bearer_token()
    {
        var client = fixture.Factory.CreateClient();
        var signIn = await client.SignInAsync("handler.alex");

        var response = await client.WithBearer(signIn.AccessToken).GetAsync("/test/probe/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = (await response.Content.ReadFromJsonAsync<ProbeController.ProbeUser>(TestAuth.Json))!;
        me.UserId.ShouldBe(signIn.User.Id);
        me.DisplayName.ShouldBe("Alex Carter");
        me.Role.ShouldBe(nameof(UserRole.Handler));
        me.OrganisationId.ShouldBe(signIn.User.OrganisationId);
    }

    [Fact]
    public async Task API_28_Dev_token_carries_sub_name_role_and_org_claims_and_expires_after_8_hours()
    {
        var signIn = await fixture.Factory.CreateClient().SignInAsync("supervisor.casey");

        var token = new JsonWebTokenHandler().ReadJsonWebToken(signIn.AccessToken);

        token.Alg.ShouldBe("HS256");
        token.Subject.ShouldBe(signIn.User.Id.ToString());
        token.GetClaim("name").Value.ShouldBe("Casey Morgan");
        token.GetClaim("role").Value.ShouldBe("supervisor");
        token.GetClaim("org").Value.ShouldBe(signIn.User.OrganisationId.ToString());
        (token.ValidTo - token.IssuedAt).ShouldBe(TimeSpan.FromHours(8));
        signIn.TokenType.ShouldBe("Bearer");
        signIn.ExpiresAt.UtcDateTime.ShouldBe(token.ValidTo, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("nobody.here")]
    [InlineData("handler.alex.old")]
    public async Task API_28_Unknown_user_gets_401(string username)
    {
        var response = await fixture.Factory.CreateClient().PostAsJsonAsync("/api/auth/dev-token", new { username });

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Unauthorized");
    }

    [Fact]
    public async Task API_28_Endpoints_are_absent_when_dev_tokens_are_disabled()
    {
        await using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("Auth:DevTokensEnabled", "false"));
        var client = factory.CreateClient();

        (await client.PostAsJsonAsync("/api/auth/dev-token", new { username = "handler.alex" })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/auth/users")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SEC_05_Two_seeded_users_per_role_in_one_organisation()
    {
        var users = await fixture.Factory.CreateClient().GetFromJsonAsync<List<UserDto>>("/api/auth/users", TestAuth.Json);

        users.ShouldNotBeNull();
        users.Select(user => user.Username).ShouldBe(
        [
            "handler.alex", "handler.blake",
            "supervisor.casey", "supervisor.drew",
            "manager.emery", "manager.finley",
        ]);
        users.GroupBy(user => user.Role).ShouldAllBe(group => group.Count() == 2);
        users.Select(user => user.OrganisationId).Distinct().ShouldHaveSingleItem();
    }
}
