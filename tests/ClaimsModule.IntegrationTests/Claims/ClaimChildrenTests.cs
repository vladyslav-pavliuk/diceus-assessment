using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ClaimsModule.Application.Claims;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.IntegrationTests.Fixtures;

namespace ClaimsModule.IntegrationTests.Claims;

/// <summary>Parties, risk objects and validation issues of a claim through HTTP (FRS §10.1, D-07, D-08, D-40).</summary>
[Collection(ApiCollection.Name)]
public sealed class ClaimChildrenTests(ApiFixture fixture) : IAsyncLifetime
{
    private ClaimsApi _api = null!;

    public async Task InitializeAsync() => _api = await ClaimsApi.SignInAsync(fixture.Factory, "handler.blake");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task API_06_Adding_a_claimant_returns_201_and_resolves_the_BR_C_03_issue()
    {
        var body = await _api.ValidFnolAsync();
        body["parties"] = new JsonArray();
        var created = await _api.CreateClaimAsync(body);

        var response = await _api.Client.PostAsJsonAsync($"/api/claims/{created.Id}/parties", new
        {
            role = "Claimant", type = "Company", companyName = "Northwind Logistics Ltd", email = "claims@northwind.example",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var party = (await response.Content.ReadFromJsonAsync<ClaimPartyDto>(TestAuth.Json))!;
        party.DisplayName.ShouldBe("Northwind Logistics Ltd");
        party.IsActive.ShouldBeTrue();

        var detail = await _api.GetDetailAsync(created.Id);
        detail.ValidationIssues.Single(issue => issue.RuleCode == "BR-C-03").Status.ShouldBe(IssueStatus.Resolved);

        var eventTypes = (await _api.AuditAsync(created.Id)).Select(entry => entry.EventType).ToList();
        eventTypes.ShouldContain(AuditEventTypes.PartyAdded); // AUD-03
        eventTypes.ShouldContain(AuditEventTypes.ValidationIssueResolved);
    }

    [Fact]
    public async Task API_06_Invalid_party_returns_422_keyed_by_body_field()
    {
        var created = await _api.CreateClaimAsync();

        var response = await _api.Client.PostAsJsonAsync($"/api/claims/{created.Id}/parties", new { role = "Claimant", type = "Person", firstName = "Sam" });

        (await ClaimsApi.ErrorsAsync(response))["LastName"].ShouldBe(["First name and last name are required for a person."]);
    }

    [Fact]
    public async Task PTY_01_Removing_the_last_active_claimant_returns_422()
    {
        var created = await _api.CreateClaimAsync();
        var claimant = (await _api.GetDetailAsync(created.Id)).Parties.Single();

        var response = await _api.Client.DeleteAsync($"/api/claims/{created.Id}/parties/{claimant.Id}");

        (await ClaimsApi.ErrorsAsync(response))["ClaimParties"].ShouldBe(["The last active Claimant cannot be removed."]);
    }

    [Fact]
    public async Task API_07_Removing_a_party_soft_removes_it_and_audits_PARTY_REMOVED()
    {
        var created = await _api.CreateClaimAsync();
        var witness = await (await _api.Client.PostAsJsonAsync($"/api/claims/{created.Id}/parties", ClaimsApi.ClaimantPerson("Witness")))
            .Content.ReadFromJsonAsync<ClaimPartyDto>(TestAuth.Json);

        var response = await _api.Client.DeleteAsync($"/api/claims/{created.Id}/parties/{witness!.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _api.GetDetailAsync(created.Id)).Parties.Single(party => party.Id == witness.Id).IsActive.ShouldBeFalse(); // D-27
        var removed = (await _api.AuditAsync(created.Id)).First();
        removed.EventType.ShouldBe(AuditEventTypes.PartyRemoved); // AUD-04
        removed.RelatedEntityId.ShouldBe(witness.Id);
    }

    [Fact]
    public async Task API_07_Removing_an_unknown_party_returns_404()
    {
        var created = await _api.CreateClaimAsync();

        var response = await _api.Client.DeleteAsync($"/api/claims/{created.Id}/parties/{Guid.NewGuid()}");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound");
    }

    [Fact]
    public async Task API_30_Adding_a_risk_object_after_intake_resolves_the_no_risk_object_warning()
    {
        var body = await _api.ValidFnolAsync();
        body.Remove("riskObjects");
        var created = await _api.CreateClaimAsync(body);
        created.ValidationIssues.ShouldHaveSingleItem().RuleCode.ShouldBe("NO-RISK-OBJECT");

        var response = await _api.Client.PostAsJsonAsync(
            $"/api/claims/{created.Id}/risk-objects", new { assetType = "Property", assetDescription = "Warehouse roof", damageDescription = "Storm damage" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<RiskObjectDto>(TestAuth.Json))!.IsPrimary.ShouldBeTrue(); // the first one (D-33)
        (await _api.GetDetailAsync(created.Id)).ValidationIssues.ShouldHaveSingleItem().Status.ShouldBe(IssueStatus.Resolved);
        (await _api.AuditAsync(created.Id)).Select(entry => entry.EventType).ShouldContain(AuditEventTypes.RiskObjectAdded);
    }

    [Fact]
    public async Task API_30_Invalid_risk_object_returns_422_keyed_by_body_field()
    {
        var created = await _api.CreateClaimAsync();

        var response = await _api.Client.PostAsJsonAsync($"/api/claims/{created.Id}/risk-objects", new { assetType = "Boat" });

        var errors = await ClaimsApi.ErrorsAsync(response);
        errors["AssetType"].ShouldBe(["Invalid asset type."]);
        errors["AssetDescription"].ShouldBe(["Asset description is required."]);
    }

    [Fact]
    public async Task API_26_Only_warnings_can_be_acknowledged_and_a_note_is_required()
    {
        var body = await _api.ValidFnolAsync();
        body["parties"] = new JsonArray();
        var created = await _api.CreateClaimAsync(body);
        var critical = created.ValidationIssues.Single(issue => issue.RuleCode == "BR-C-03");

        var withoutNote = await _api.Client.PostAsJsonAsync($"/api/claims/{created.Id}/validation-issues/{critical.Id}/acknowledge", new { note = " " });
        (await ClaimsApi.ErrorsAsync(withoutNote))["Note"].ShouldBe(["An acknowledgement note is required."]);

        var onCritical = await _api.Client.PostAsJsonAsync($"/api/claims/{created.Id}/validation-issues/{critical.Id}/acknowledge", new { note = "Accepting." });
        (await ClaimsApi.ErrorsAsync(onCritical))["ValidationIssue"].ShouldBe(["Only a Warning can be acknowledged."]);
    }

    [Fact]
    public async Task API_26_Acknowledging_a_warning_is_audited_and_listed()
    {
        var created = await _api.CreateClaimAsync(await _api.ValidFnolAsync(ClaimsApi.ExpiredPolicy));
        var warning = created.ValidationIssues.Single();

        var response = await _api.Client.PostAsJsonAsync(
            $"/api/claims/{created.Id}/validation-issues/{warning.Id}/acknowledge", new { note = "Renewal confirmed by the broker." });

        var acknowledged = (await response.Content.ReadFromJsonAsync<ValidationIssueDto>(TestAuth.Json))!;
        acknowledged.Status.ShouldBe(IssueStatus.Acknowledged);
        acknowledged.ResolutionNote.ShouldBe("Renewal confirmed by the broker.");

        var listed = await _api.Client.GetFromJsonAsync<List<ValidationIssueDto>>($"/api/claims/{created.Id}/validation-issues", TestAuth.Json);
        listed!.ShouldHaveSingleItem().Status.ShouldBe(IssueStatus.Acknowledged);
        (await _api.AuditAsync(created.Id)).First().EventType.ShouldBe(AuditEventTypes.ValidationIssueAcknowledged);
    }

    [Fact]
    public async Task API_20_Validate_returns_every_issue_of_the_claim()
    {
        var created = await _api.CreateClaimAsync(ClaimsApi.FnolBody(policyId: null));

        var response = await _api.Client.PostAsync($"/api/claims/{created.Id}/validate", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<ValidationIssueDto>>(TestAuth.Json))!
            .ShouldHaveSingleItem().RuleCode.ShouldBe("BR-C-06");
    }

    [Fact]
    public async Task API_21_Linking_a_policy_resolves_BR_C_06_and_an_expired_one_raises_BR_C_02()
    {
        var created = await _api.CreateClaimAsync(ClaimsApi.FnolBody(policyId: null));

        var expired = await _api.Client.PutAsJsonAsync($"/api/claims/{created.Id}/policy", new { policyId = await _api.PolicyIdAsync(ClaimsApi.ExpiredPolicy) });
        expired.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var issues = (await _api.GetDetailAsync(created.Id)).ValidationIssues;
        issues.Single(issue => issue.RuleCode == "BR-C-06").Status.ShouldBe(IssueStatus.Resolved);
        issues.Single(issue => issue.RuleCode == "BR-C-02").Status.ShouldBe(IssueStatus.Open);

        await _api.Client.PutAsJsonAsync($"/api/claims/{created.Id}/policy", new { policyId = await _api.PolicyIdAsync(ClaimsApi.InForcePolicy) });
        var detail = await _api.GetDetailAsync(created.Id);
        detail.PolicyNumber.ShouldBe(ClaimsApi.InForcePolicy);
        detail.ValidationIssues.ShouldAllBe(issue => issue.Status == IssueStatus.Resolved);
        (await _api.AuditAsync(created.Id)).Select(entry => entry.EventType).ShouldContain(AuditEventTypes.PolicyLinked);
    }

    [Theory]
    [InlineData(false, "A policy is required.")]
    [InlineData(true, "Policy was not found.")]
    public async Task API_21_Missing_or_unknown_policy_returns_422(bool sendUnknownId, string message)
    {
        var created = await _api.CreateClaimAsync();

        var response = await _api.Client.PutAsJsonAsync(
            $"/api/claims/{created.Id}/policy", new { policyId = sendUnknownId ? Guid.NewGuid() : (Guid?)null });

        (await ClaimsApi.ErrorsAsync(response))["PolicyId"].ShouldBe([message]);
    }

    [Fact]
    public async Task API_22_Supervisor_reassigns_the_handler_and_a_handler_gets_403()
    {
        var created = await _api.CreateClaimAsync();
        var supervisor = await ClaimsApi.SignInAsync(fixture.Factory, "supervisor.drew");
        var alex = (await supervisor.Client.SignInAsync("handler.alex")).User.Id;

        (await _api.Client.PutAsJsonAsync($"/api/claims/{created.Id}/assignee", new { userId = alex }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await supervisor.Client.PutAsJsonAsync($"/api/claims/{created.Id}/assignee", new { userId = alex }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await supervisor.GetDetailAsync(created.Id)).AssignedHandlerName.ShouldBe("Alex Carter");
        (await supervisor.AuditAsync(created.Id)).First().EventType.ShouldBe(AuditEventTypes.HandlerAssigned);

        var unknown = await supervisor.Client.PutAsJsonAsync($"/api/claims/{created.Id}/assignee", new { userId = Guid.NewGuid() });
        (await ClaimsApi.ErrorsAsync(unknown))["AssignedHandlerId"].ShouldBe(["User was not found."]);
    }

    [Fact]
    public async Task API_23_Patch_updates_notes_and_severity_and_audits_each_change()
    {
        var created = await _api.CreateClaimAsync();

        var response = await _api.Client.PatchAsJsonAsync($"/api/claims/{created.Id}", new { notes = "Called the claimant.", severity = "Critical" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var detail = await _api.GetDetailAsync(created.Id);
        detail.Notes.ShouldBe("Called the claimant.");
        detail.Severity.ShouldBe(ClaimSeverity.Critical);
        (await _api.AuditAsync(created.Id)).Take(2).Select(entry => entry.EventType).ShouldBe([AuditEventTypes.ClaimUpdated, AuditEventTypes.ClaimUpdated]);

        // A null field is left alone; an empty string clears the notes.
        await _api.Client.PatchAsJsonAsync($"/api/claims/{created.Id}", new { notes = "" });
        detail = await _api.GetDetailAsync(created.Id);
        detail.Notes.ShouldBeNull();
        detail.Severity.ShouldBe(ClaimSeverity.Critical);
    }
}
