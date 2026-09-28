using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Policies;
using ClaimsModule.Application.ReferenceData;
using ClaimsModule.Application.Users;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Policies;
using ClaimsModule.Domain.ReferenceData;
using ClaimsModule.Domain.Users;
using ClaimsModule.IntegrationTests.Fixtures;

namespace ClaimsModule.IntegrationTests.Reference;

/// <summary>Reference data, policy lookup and users (FRS §10.3, brief §3.3.2, D-08).</summary>
[Collection(ApiCollection.Name)]
public sealed class ReferenceEndpointTests(ApiFixture fixture) : IAsyncLifetime
{
    private ClaimsApi _api = null!;

    public async Task InitializeAsync() => _api = await ClaimsApi.SignInAsync(fixture.Factory, "handler.alex");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task API_16_Active_cause_codes_in_order_and_filtered_by_peril()
    {
        var all = await GetAsync<List<CauseOfLossCodeDto>>("/api/reference/cause-of-loss-codes");
        all.Select(code => code.Code).ShouldBe(
            ["COL-FIRE", "COL-FLOOD", "COL-THEFT", "COL-VEH-COL", "COL-VEH-COMP", "COL-LIAB", "COL-EQUIP", "COL-WIND", "COL-INJURY", "COL-OTHER"]);

        var weather = await GetAsync<List<CauseOfLossCodeDto>>("/api/reference/cause-of-loss-codes?perilCategory=Weather");
        weather.Select(code => code.Code).ShouldBe(["COL-FLOOD", "COL-WIND"]);
        weather.ShouldAllBe(code => code.PerilCategory == PerilCategory.Weather);
    }

    [Fact]
    public async Task API_16_Unknown_peril_category_returns_422()
    {
        var response = await _api.Client.GetAsync("/api/reference/cause-of-loss-codes?perilCategory=Volcano");

        (await ClaimsApi.ErrorsAsync(response)).Keys.ShouldHaveSingleItem().ShouldBe("perilCategory");
    }

    [Fact]
    public async Task API_17_Statuses_include_the_seeded_transitions()
    {
        var statuses = await GetAsync<List<ClaimStatusDto>>("/api/reference/claim-statuses");

        statuses.Select(status => status.Status).ShouldBe(Enum.GetValues<ClaimStatus>());
        statuses.Single(status => status.Status == ClaimStatus.Draft).Transitions.Select(t => t.ToStatus).ShouldBe([ClaimStatus.Open]);
        statuses.Single(status => status.Status == ClaimStatus.Withdrawn).Transitions.ShouldBeEmpty();

        var reopen = statuses.Single(status => status.Status == ClaimStatus.Closed).Transitions.ShouldHaveSingleItem();
        reopen.ToStatus.ShouldBe(ClaimStatus.Reopened);
        reopen.MinimumRole.ShouldBe(UserRole.Supervisor); // BR-ST-04
        reopen.RequiresReason.ShouldBeTrue();

        var automatic = statuses.Single(status => status.Status == ClaimStatus.Reopened).Transitions.ShouldHaveSingleItem();
        automatic.IsSystemOnly.ShouldBeTrue();
        automatic.MinimumRole.ShouldBeNull();
    }

    [Theory]
    [InlineData("POL-2024-0010", new[] { "POL-2024-001001", "POL-2024-001002" })]
    [InlineData("harborview", new[] { "POL-2024-001002" })]
    [InlineData("Medical", new[] { "POL-2025-002002" })]
    public async Task API_18_Policy_search_matches_number_or_client_name(string term, string[] expected)
    {
        var policies = await GetAsync<List<PolicyDto>>($"/api/policies/search?q={term}");

        policies.Select(policy => policy.PolicyNumber).ShouldBe(expected);
    }

    [Fact]
    public async Task API_18_Search_result_carries_dates_status_and_coverage()
    {
        var archived = (await GetAsync<List<PolicyDto>>("/api/policies/search?q=POL-2023-000099")).ShouldHaveSingleItem();

        archived.ClientName.ShouldBe("Archived Corp");
        archived.EffectiveDate.ShouldBe(new DateOnly(2020, 1, 1));
        archived.ExpirationDate.ShouldBe(new DateOnly(2021, 12, 31));
        archived.Status.ShouldBe(PolicyStatus.Expired);
        archived.CoverageTypes.ShouldBe(["Property"]);
    }

    [Fact]
    public async Task API_18_Search_term_is_required()
    {
        (await ClaimsApi.ErrorsAsync(await _api.Client.GetAsync("/api/policies/search?q=")))["Q"].ShouldBe(["A search term is required."]);
    }

    [Fact]
    public async Task API_19_Policy_coverage_and_404_for_unknown_policy()
    {
        var coverage = await GetAsync<PolicyCoverageDto>($"/api/policies/{await _api.PolicyIdAsync("POL-2024-001001")}/coverage");
        coverage.CoverageTypes.ShouldBe(["Vehicle", "Cargo"]);

        await (await _api.Client.GetAsync($"/api/policies/{Guid.NewGuid()}/coverage")).ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound");
    }

    [Fact]
    public async Task API_29_Users_filtered_by_role()
    {
        var handlers = await GetAsync<List<UserDto>>("/api/users?role=handler");
        handlers.Select(user => user.Username).ShouldBe(["handler.alex", "handler.blake"]);

        (await GetAsync<List<UserDto>>("/api/users")).Count.ShouldBe(6);
    }

    private async Task<T> GetAsync<T>(string url)
    {
        var response = await _api.Client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(TestAuth.Json))!;
    }
}
