using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Claims.Commands.CreateClaim;
using ClaimsModule.IntegrationTests.Fixtures;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.Idempotency;

/// <summary>The Idempotency-Key header on write endpoints (FRS §10, D-24).</summary>
[Collection(ApiCollection.Name)]
public sealed class IdempotencyTests(ApiFixture fixture) : IAsyncLifetime
{
    private ClaimsApi _api = null!;

    public async Task InitializeAsync() => _api = await ClaimsApi.SignInAsync(fixture.Factory, "handler.alex");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task API_IDEMP_Repeated_key_replays_the_response_and_creates_one_claim()
    {
        var body = await _api.ValidFnolAsync();
        var key = Guid.NewGuid().ToString();

        var first = await _api.PostClaimAsync(body, key);
        var second = await _api.PostClaimAsync(body, key);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.Headers.GetValues("Idempotency-Replayed").ShouldBe(["true"]);
        second.Headers.Location.ShouldBe(first.Headers.Location);
        (await second.Content.ReadAsStringAsync()).ShouldBe(await first.Content.ReadAsStringAsync());

        var claimNumber = (await first.Content.ReadFromJsonAsync<ClaimCreatedDto>(TestAuth.Json))!.ClaimNumber;
        await using var scope = await new TestDatabase(fixture).TenantScopeAsync();
        (await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Claims.CountAsync(claim => claim.ClaimNumber == claimNumber)).ShouldBe(1);
    }

    [Fact]
    public async Task API_IDEMP_Same_key_with_a_different_body_returns_422()
    {
        var key = Guid.NewGuid().ToString();
        (await _api.PostClaimAsync(await _api.ValidFnolAsync(), key)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var different = await _api.ValidFnolAsync();
        different["lossLocation"] = "Somewhere else entirely";

        (await ClaimsApi.ErrorsAsync(await _api.PostClaimAsync(different, key)))["Idempotency-Key"]
            .ShouldBe(["Idempotency-Key reuse with a different request."]);
    }

    [Fact]
    public async Task API_IDEMP_A_failed_request_does_not_consume_the_key()
    {
        var key = Guid.NewGuid().ToString();
        var invalid = await _api.ValidFnolAsync();
        invalid["lossDescription"] = "Too short";
        (await _api.PostClaimAsync(invalid, key)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        (await _api.PostClaimAsync(await _api.ValidFnolAsync(), key)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task API_IDEMP_Keys_are_scoped_to_the_user()
    {
        var key = Guid.NewGuid().ToString();
        var blake = await ClaimsApi.SignInAsync(fixture.Factory, "handler.blake");

        var mine = await _api.PostClaimAsync(await _api.ValidFnolAsync(), key);
        var theirs = await blake.PostClaimAsync(await blake.ValidFnolAsync(), key);

        theirs.StatusCode.ShouldBe(HttpStatusCode.Created);
        theirs.Headers.Contains("Idempotency-Replayed").ShouldBeFalse();
        theirs.Headers.Location.ShouldNotBe(mine.Headers.Location);
    }

    [Fact]
    public async Task API_IDEMP_A_request_still_in_progress_returns_409()
    {
        var key = Guid.NewGuid().ToString();
        var json = (await _api.ValidFnolAsync()).ToJsonString();
        var userId = (await _api.Client.SignInAsync("handler.alex")).User.Id;

        // The placeholder a first, still-running request would have written.
        await using (var scope = await new TestDatabase(fixture).TenantScopeAsync())
        {
            var hash = Convert.ToHexString(SHA256.HashData(
                [.. Encoding.UTF8.GetBytes("POST\n/api/claims\n"), .. SHA256.HashData(Encoding.UTF8.GetBytes(json))]));
            var begun = await scope.ServiceProvider.GetRequiredService<IIdempotencyStore>()
                .BeginAsync(new IdempotentRequest(userId, key, "POST", "/api/claims", hash), CancellationToken.None);
            begun.ShouldBeOfType<IdempotencyClaim.Started>();
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/claims") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Add("Idempotency-Key", key);
        var response = await _api.Client.SendAsync(request);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Conflict");
    }

    [Fact]
    public async Task API_IDEMP_Key_longer_than_200_characters_returns_422()
    {
        (await ClaimsApi.ErrorsAsync(await _api.PostClaimAsync(await _api.ValidFnolAsync(), new string('k', 201))))["Idempotency-Key"]
            .ShouldBe(["Idempotency-Key must be 1 to 200 characters."]);
    }
}
