using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Claims.Commands.CreateClaim;
using ClaimsModule.Application.Common.Paging;
using ClaimsModule.Application.Policies;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ClaimsModule.IntegrationTests.Fixtures;

/// <summary>
/// HTTP helpers for the claims endpoints. Request bodies are built as JSON nodes, so a test can send
/// exactly what a client would, including values the typed commands cannot hold (an unknown enum name,
/// a malformed date).
/// </summary>
internal sealed class ClaimsApi(HttpClient client)
{
    /// <summary>A policy whose period (2025-01-01 → 2030-12-31) covers every loss date the tests use (D-34).</summary>
    public const string InForcePolicy = "POL-2025-003001";

    /// <summary>FRS §5.5: expired in 2021, so any recent loss date is outside its period (BR-C-02).</summary>
    public const string ExpiredPolicy = "POL-2023-000099";

    public HttpClient Client => client;

    public static async Task<ClaimsApi> SignInAsync(WebApplicationFactory<Program> factory, string username)
    {
        var client = factory.CreateClient();
        var token = await client.SignInAsync(username);
        return new ClaimsApi(client.WithBearer(token.AccessToken));
    }

    public async Task<Guid> PolicyIdAsync(string policyNumber)
    {
        var policies = await client.GetFromJsonAsync<List<PolicyDto>>($"/api/policies/search?q={policyNumber}", TestAuth.Json);
        return policies!.Single(policy => policy.PolicyNumber == policyNumber).Id;
    }

    /// <summary>A complete FNOL body: in-force policy, one Claimant, one risk object, no initial reserve.</summary>
    public async Task<JsonObject> ValidFnolAsync(string policyNumber = InForcePolicy) => FnolBody(await PolicyIdAsync(policyNumber));

    public static JsonObject FnolBody(Guid? policyId) => new()
    {
        ["policyId"] = policyId,
        ["lossDate"] = DateTimeOffset.UtcNow.AddDays(-2).ToString("O"),
        ["lossDescription"] = "Delivery van rear-ended at a junction on the ring road.",
        ["lossLocation"] = "Ring road, junction 4",
        ["causeOfLossCode"] = "COL-VEH-COL",
        ["estimatedLossAmount"] = 4200.50m,
        ["severity"] = "Standard",
        ["parties"] = new JsonArray(ClaimantPerson()),
        ["riskObjects"] = new JsonArray(new JsonObject
        {
            ["assetType"] = "Vehicle",
            ["assetDescription"] = "2022 Ford Transit",
            ["damageDescription"] = "Rear doors and bumper",
            ["assetReference"] = "WF0XXXTTGXNA00042",
        }),
    };

    public static JsonObject ClaimantPerson(string role = "Claimant") => new()
    {
        ["role"] = role,
        ["type"] = "Person",
        ["firstName"] = "Jordan",
        ["lastName"] = "Reyes",
        ["email"] = "jordan.reyes@example.com",
        ["phone"] = "+1 555 0100",
    };

    public Task<HttpResponseMessage> PostClaimAsync(JsonNode body, string? idempotencyKey = null, Guid? correlationId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/claims") { Content = JsonContent.Create(body) };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        if (correlationId is not null)
        {
            request.Headers.Add("X-Correlation-Id", correlationId.Value.ToString());
        }

        return client.SendAsync(request);
    }

    public async Task<ClaimCreatedDto> CreateClaimAsync(JsonObject? body = null)
    {
        var response = await PostClaimAsync(body ?? await ValidFnolAsync());
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ClaimCreatedDto>(TestAuth.Json))!;
    }

    public async Task<ClaimDetailDto> GetDetailAsync(Guid claimId)
    {
        var response = await client.GetAsync($"/api/claims/{claimId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ClaimDetailDto>(TestAuth.Json))!;
    }

    public async Task<IReadOnlyList<AuditEntryDto>> AuditAsync(Guid claimId, int pageSize = 200)
    {
        var page = await client.GetFromJsonAsync<PagedResult<AuditEntryDto>>($"/api/claims/{claimId}/audit?pageSize={pageSize}", TestAuth.Json);
        return page!.Items;
    }

    public Task<HttpResponseMessage> TransitionAsync(Guid claimId, string targetStatus, string? reason = null, string? justification = null) =>
        client.PutAsJsonAsync($"/api/claims/{claimId}/status", new { targetStatus, reason, justification });

    public async Task TransitionOkAsync(Guid claimId, string targetStatus, string? reason = null, string? justification = null)
    {
        var response = await TransitionAsync(claimId, targetStatus, reason, justification);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    /// <summary>A claim that is Open: created complete, then moved out of Draft.</summary>
    public async Task<ClaimCreatedDto> CreateOpenClaimAsync(JsonObject? body = null)
    {
        var created = await CreateClaimAsync(body);
        await TransitionOkAsync(created.Id, "Open");
        return created;
    }

    public static async Task<IReadOnlyDictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response)
    {
        var body = await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ValidationError");
        body.PropertyNames().ShouldBe(["type", "title", "status", "errors"], ignoreOrder: true);
        return body.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
    }
}
