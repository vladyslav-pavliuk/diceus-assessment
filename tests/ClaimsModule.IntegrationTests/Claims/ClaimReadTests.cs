using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Common.Paging;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.IntegrationTests.Fixtures;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.Claims;

/// <summary>
/// GET /api/claims, /api/claims/{id} and /api/claims/{id}/audit (FRS §10.1, D-29). The database is shared
/// with other tests, so list assertions check the claims a test created rather than exact totals.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ClaimReadTests(ApiFixture fixture) : IAsyncLifetime
{
    private ClaimsApi _api = null!;

    public async Task InitializeAsync() => _api = await ClaimsApi.SignInAsync(fixture.Factory, "handler.alex");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task API_02_Summary_rows_carry_the_listed_fields_and_total_reserves()
    {
        var body = await _api.ValidFnolAsync();
        body["initialReserve"] = new JsonObject { ["component"] = "SubrogationRecoverable", ["amount"] = -300m };
        var created = await _api.CreateClaimAsync(body);

        var row = (await ListAsync($"search={created.ClaimNumber}")).Items.ShouldHaveSingleItem();

        row.Id.ShouldBe(created.Id);
        row.PolicyNumber.ShouldBe(ClaimsApi.InForcePolicy);
        row.ClientName.ShouldBe("Northwind Logistics Ltd");
        row.CauseOfLossCode.ShouldBe("COL-VEH-COL");
        row.CauseOfLossName.ShouldBe("Vehicle Collision");
        row.Status.ShouldBe(ClaimStatus.Draft);
        row.AssignedHandlerName.ShouldBe("Alex Carter");
        row.TotalReserves.ShouldBe(-300m); // D-29: net, Subrogation included
        row.IsSlaBreached.ShouldBeFalse();
    }

    [Fact]
    public async Task API_02_Each_filter_narrows_the_list()
    {
        var number = Guid.NewGuid().ToString("N")[..6];
        var fire = await _api.ValidFnolAsync(ClaimsApi.ExpiredPolicy);
        fire["causeOfLossCode"] = "COL-FIRE";
        fire["lossDate"] = "2024-03-15T10:00:00Z";
        fire["lossDescription"] = $"Kitchen fire in unit {number}, smoke damage.";
        var fireClaim = await _api.CreateClaimAsync(fire);
        var collision = await _api.CreateOpenClaimAsync();
        var mine = new[] { fireClaim.Id, collision.Id };

        async Task<IReadOnlyList<Guid>> Ids(string query) =>
            (await ListAsync(query + "&pageSize=100")).Items.Select(item => item.Id).Where(mine.Contains).ToList();

        (await Ids("status=Draft")).ShouldBe([fireClaim.Id]);
        (await Ids("status=Draft&status=Open")).ShouldBe(mine, ignoreOrder: true);
        (await Ids("causeOfLossCode=COL-FIRE")).ShouldBe([fireClaim.Id]);
        (await Ids($"policyId={await _api.PolicyIdAsync(ClaimsApi.ExpiredPolicy)}")).ShouldBe([fireClaim.Id]);
        (await Ids("dateFrom=2024-03-15&dateTo=2024-03-15")).ShouldBe([fireClaim.Id]); // inclusive, UTC calendar date (D-32)
        (await Ids("dateTo=2024-03-14")).ShouldBeEmpty();
        (await Ids("search=Archived")).ShouldContain(fireClaim.Id); // part of the client name
        (await Ids($"search={collision.ClaimNumber[^7..]}")).ShouldBe([collision.Id]); // part of the claim number

        var blake = await ClaimsApi.SignInAsync(fixture.Factory, "handler.blake");
        var blakesClaim = await blake.CreateClaimAsync();
        var blakeId = (await blake.GetDetailAsync(blakesClaim.Id)).AssignedHandlerId;
        (await ListAsync($"assignedHandlerId={blakeId}&pageSize=100")).Items.ShouldAllBe(item => item.AssignedHandlerId == blakeId);
    }

    [Fact]
    public async Task API_02_Paging_metadata_and_newest_first_order()
    {
        await _api.CreateClaimAsync();
        await _api.CreateClaimAsync();

        var firstPage = await ListAsync("page=1&pageSize=2");
        var secondPage = await ListAsync("page=2&pageSize=2");

        firstPage.Page.ShouldBe(1);
        firstPage.PageSize.ShouldBe(2);
        firstPage.Items.Count.ShouldBe(2);
        firstPage.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
        secondPage.TotalCount.ShouldBe(firstPage.TotalCount);
        firstPage.Items[0].ReportedDate.ShouldBeGreaterThanOrEqualTo(firstPage.Items[1].ReportedDate);
        secondPage.Items.Select(item => item.Id).ShouldNotContain(firstPage.Items[0].Id);
    }

    [Theory]
    [InlineData("page=0", "Page", "Page must be 1 or greater.")]
    [InlineData("pageSize=101", "PageSize", "Page size must be between 1 and 100.")]
    [InlineData("dateFrom=2026-02-01&dateTo=2026-01-01", "DateTo", "dateFrom must not be later than dateTo.")]
    public async Task API_02_Invalid_list_parameters_return_422(string query, string key, string message)
    {
        var response = await _api.Client.GetAsync($"/api/claims?{query}");

        (await ClaimsApi.ErrorsAsync(response))[key].ShouldBe([message]);
    }

    [Fact]
    public async Task API_03_Detail_includes_children_reserve_summary_and_recent_audit()
    {
        var body = await _api.ValidFnolAsync();
        body["initialReserve"] = new JsonObject { ["component"] = "Indemnity", ["amount"] = 25_000m };
        var created = await _api.CreateClaimAsync(body);

        var detail = await _api.GetDetailAsync(created.Id);

        detail.LossEvent.PerilCategory.ShouldBe(Domain.ReferenceData.PerilCategory.Auto); // D-13 "claim type"
        detail.LossEvent.EstimatedLossAmount.ShouldBe(4200.50m);
        detail.Parties.Count.ShouldBe(1);
        detail.RiskObjects.Count.ShouldBe(1);
        var indemnity = detail.ReserveComponents.ShouldHaveSingleItem();
        indemnity.CurrentAmount.ShouldBe(0m);
        indemnity.PendingAmount.ShouldBe(25_000m); // D-11: not approved yet
        indemnity.HasPendingApproval.ShouldBeTrue();
        detail.TotalReserves.ShouldBe(0m);
        detail.Documents.ShouldBeEmpty();
        detail.RecentAuditEntries.Select(entry => entry.EventType).ShouldContain(AuditEventTypes.ClaimCreated);
        detail.RecentAuditEntries.ShouldBeInOrder(SortDirection.Descending, Comparer<AuditEntryDto>.Create((a, b) => a.CreatedAt.CompareTo(b.CreatedAt)));
    }

    [Fact]
    public async Task API_05_Audit_is_reverse_chronological_and_paged()
    {
        var created = await _api.CreateOpenClaimAsync();
        var all = await _api.AuditAsync(created.Id);

        var page = await _api.Client.GetFromJsonAsync<PagedResult<AuditEntryDto>>($"/api/claims/{created.Id}/audit?page=2&pageSize=2", TestAuth.Json);

        page!.TotalCount.ShouldBe(all.Count);
        page.Items.Select(entry => entry.Id).ShouldBe(all.Skip(2).Take(2).Select(entry => entry.Id));
        all[0].EventType.ShouldBe(AuditEventTypes.StatusChanged); // the newest: Draft → Open
        all[^1].EventType.ShouldBe(AuditEventTypes.ClaimCreated); // the oldest
    }

    [Theory]
    [InlineData("")]
    [InlineData("/audit")]
    [InlineData("/validation-issues")]
    public async Task API_03_Unknown_claim_returns_404(string suffix)
    {
        var response = await _api.Client.GetAsync($"/api/claims/{Guid.NewGuid()}{suffix}");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound");
    }

    /// <summary>SEC-04 over HTTP: another organisation's claim is invisible, so it is "not found", not "forbidden".</summary>
    [Fact]
    public async Task SEC_04_Cross_tenant_claim_is_not_found()
    {
        var otherOrganisation = Guid.NewGuid();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ClaimsDbContext>().Database.ExecuteSqlAsync(
                $"INSERT INTO [Organisations] ([Id], [Name], [CreatedAt]) VALUES ({otherOrganisation}, N'Rival Mutual', SYSDATETIMEOFFSET())");
        }

        // A claim that belongs to our organisation, moved into the other one directly in the database.
        var foreign = await _api.CreateClaimAsync();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
            await dbContext.Database.ExecuteSqlAsync($"UPDATE [Claims] SET [OrganisationId] = {otherOrganisation} WHERE [Id] = {foreign.Id}");
        }

        (await _api.Client.GetAsync($"/api/claims/{foreign.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _api.TransitionAsync(foreign.Id, "Open")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ListAsync($"search={foreign.ClaimNumber}")).Items.ShouldBeEmpty();
    }

    private async Task<PagedResult<ClaimSummaryDto>> ListAsync(string query)
    {
        var response = await _api.Client.GetAsync($"/api/claims?{query}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PagedResult<ClaimSummaryDto>>(TestAuth.Json))!;
    }
}
