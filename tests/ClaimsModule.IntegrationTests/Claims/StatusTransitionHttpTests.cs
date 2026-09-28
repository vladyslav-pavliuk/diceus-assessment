using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ClaimsModule.Application.Claims.Commands.TransitionClaimStatus;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.IntegrationTests.Fixtures;

namespace ClaimsModule.IntegrationTests.Claims;

/// <summary>PUT /api/claims/{id}/status (FRS §4.2, §7.3, §10.1) through HTTP.</summary>
[Collection(ApiCollection.Name)]
public sealed class StatusTransitionHttpTests(ApiFixture fixture) : IAsyncLifetime
{
    private ClaimsApi _handler = null!;
    private ClaimsApi _supervisor = null!;

    public async Task InitializeAsync()
    {
        _handler = await ClaimsApi.SignInAsync(fixture.Factory, "handler.alex");
        _supervisor = await ClaimsApi.SignInAsync(fixture.Factory, "supervisor.casey");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TR_01_Draft_to_open_returns_200_and_audits_old_and_new_status()
    {
        var created = await _handler.CreateClaimAsync();

        var response = await _handler.TransitionAsync(created.Id, "Open");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var changed = (await response.Content.ReadFromJsonAsync<ClaimStatusChangedDto>(TestAuth.Json))!;
        changed.ShouldBe(new ClaimStatusChangedDto(created.Id, ClaimStatus.Draft, ClaimStatus.Open));

        var entry = (await _handler.AuditAsync(created.Id)).First(); // newest first
        entry.EventType.ShouldBe(AuditEventTypes.StatusChanged); // AUD-02
        entry.OldValue.ShouldBe("{\"status\":\"Draft\"}");
        entry.NewValue.ShouldBe("{\"status\":\"Open\",\"reason\":null}");
    }

    [Fact]
    public async Task BR_ST_01_Invalid_transition_returns_422_listing_the_valid_next_statuses()
    {
        var created = await _handler.CreateClaimAsync();

        // Also B-C-06: Draft may not go straight to Closed.
        var errors = await ClaimsApi.ErrorsAsync(await _handler.TransitionAsync(created.Id, "Closed", reason: "Done"));

        errors["StatusTransition"].ShouldBe(["Transition from Draft to Closed is not permitted.", "Valid next statuses: Open."]);
    }

    [Theory]
    [InlineData(null, "Target status is required.")]
    [InlineData("Archived", "Invalid claim status.")]
    public async Task BR_ST_01_Missing_or_unknown_target_status_returns_422(string? target, string message)
    {
        var created = await _handler.CreateClaimAsync();

        var response = await _handler.Client.PutAsJsonAsync($"/api/claims/{created.Id}/status", new JsonObject { ["targetStatus"] = target });

        (await ClaimsApi.ErrorsAsync(response))["TargetStatus"].ShouldBe([message]);
    }

    [Fact]
    public async Task BR_ST_02_Open_without_an_active_claimant_returns_422()
    {
        var body = await _handler.ValidFnolAsync();
        body["parties"] = new JsonArray();
        var created = await _handler.CreateClaimAsync(body);

        var errors = await ClaimsApi.ErrorsAsync(await _handler.TransitionAsync(created.Id, "Open"));

        errors["ClaimParties"].ShouldBe(["At least one Claimant party is required to open a claim."]);
    }

    [Fact]
    public async Task BR_C_02_Unacknowledged_period_warning_blocks_open_until_acknowledged()
    {
        var created = await _handler.CreateClaimAsync(await _handler.ValidFnolAsync(ClaimsApi.ExpiredPolicy));
        var issueId = created.ValidationIssues.Single(issue => issue.RuleCode == "BR-C-02").Id;

        (await ClaimsApi.ErrorsAsync(await _handler.TransitionAsync(created.Id, "Open")))["LossDate"].ShouldBe(
            ["Loss date is outside the policy effective period. Acknowledge the warning or link a policy that covers the loss date before opening the claim."]);

        var acknowledged = await _handler.Client.PostAsJsonAsync(
            $"/api/claims/{created.Id}/validation-issues/{issueId}/acknowledge", new { note = "Policy renewal is being confirmed with the broker." });
        acknowledged.StatusCode.ShouldBe(HttpStatusCode.OK);

        await _handler.TransitionOkAsync(created.Id, "Open"); // D-19
    }

    [Fact]
    public async Task TR_04_Close_without_reason_returns_422()
    {
        var created = await _handler.CreateOpenClaimAsync();

        (await ClaimsApi.ErrorsAsync(await _handler.TransitionAsync(created.Id, "Closed")))["Reason"]
            .ShouldBe(["A reason is required to move a claim to Closed."]);
    }

    [Fact]
    public async Task CC_01_Close_with_a_pending_reserve_returns_422_with_the_condition()
    {
        var body = await _handler.ValidFnolAsync();
        body["initialReserve"] = new JsonObject { ["component"] = "Indemnity", ["amount"] = 50_000m };
        var created = await _handler.CreateOpenClaimAsync(body);

        var errors = await ClaimsApi.ErrorsAsync(await _handler.TransitionAsync(created.Id, "Closed", reason: "Settled"));

        errors["StatusTransition"].ShouldBe(["Claim cannot be closed — CC-01 (no reserve transaction is pending approval) is not satisfied."]);
    }

    [Fact]
    public async Task CC_04_Close_with_open_reserves_needs_a_justification_then_audits_the_closure()
    {
        var body = await _handler.ValidFnolAsync();
        body["initialReserve"] = new JsonObject { ["component"] = "Indemnity", ["amount"] = 2_500m };
        var created = await _handler.CreateOpenClaimAsync(body);

        var errors = await ClaimsApi.ErrorsAsync(await _handler.TransitionAsync(created.Id, "Closed", reason: "Settled"));
        errors["OpenReserves"].ShouldBe(
        [
            "The claim has open reserves totalling 2500.00.",
            "Claim cannot be closed — CC-04 (a justification note is required to close a claim with open reserves) is not satisfied.",
        ]);

        await _handler.TransitionOkAsync(created.Id, "Closed", reason: "Settled", justification: "Payment made outside the system.");

        var detail = await _handler.GetDetailAsync(created.Id);
        detail.Status.ShouldBe(ClaimStatus.Closed);
        detail.ClosureReason.ShouldBe("Settled");
        detail.ClosedAt.ShouldNotBeNull();

        var audit = await _handler.AuditAsync(created.Id);
        audit[0].EventType.ShouldBe(AuditEventTypes.ClaimClosed); // AUD-13
        audit[0].NewValue.ShouldBe("{\"closureReason\":\"Settled\",\"justification\":\"Payment made outside the system.\",\"openReserveTotal\":2500.0000}");
        audit[1].EventType.ShouldBe(AuditEventTypes.StatusChanged);
    }

    [Fact]
    public async Task TR_03_Pending_payment_needs_an_approved_reserve()
    {
        var withoutReserve = await _handler.CreateOpenClaimAsync();
        (await ClaimsApi.ErrorsAsync(await _handler.TransitionAsync(withoutReserve.Id, "PendingPayment")))["Reserves"]
            .ShouldBe(["At least one approved reserve is required to move a claim to PendingPayment."]);

        var body = await _handler.ValidFnolAsync();
        body["initialReserve"] = new JsonObject { ["component"] = "Expense", ["amount"] = 800m };
        var withReserve = await _handler.CreateOpenClaimAsync(body);
        await _handler.TransitionOkAsync(withReserve.Id, "PendingPayment");
    }

    [Fact]
    public async Task BR_ST_04_Handler_cannot_reopen_403()
    {
        var created = await _handler.CreateOpenClaimAsync();
        await _handler.TransitionOkAsync(created.Id, "Closed", reason: "No payment due");

        var response = await _handler.TransitionAsync(created.Id, "Reopened", reason: "New evidence");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden");
    }

    [Fact]
    public async Task BR_ST_04_Reopen_moves_to_open_and_writes_three_audit_rows()
    {
        var created = await _handler.CreateOpenClaimAsync();
        await _handler.TransitionOkAsync(created.Id, "Closed", reason: "No payment due");

        var response = await _supervisor.TransitionAsync(created.Id, "Reopened", reason: "New evidence");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ClaimStatusChangedDto>(TestAuth.Json))!.Status.ShouldBe(ClaimStatus.Open);

        var newest = (await _supervisor.AuditAsync(created.Id)).Take(3).ToList();
        newest.Select(entry => (entry.EventType, entry.NewValue)).ShouldBe(
        [
            (AuditEventTypes.StatusChanged, "{\"status\":\"Open\",\"reason\":null}"),
            (AuditEventTypes.ClaimReopened, "{\"reason\":\"New evidence\"}"), // AUD-14
            (AuditEventTypes.StatusChanged, "{\"status\":\"Reopened\",\"reason\":\"New evidence\"}"),
        ]);

        var detail = await _supervisor.GetDetailAsync(created.Id);
        detail.ClosedAt.ShouldBeNull();
        detail.ClosureReason.ShouldBeNull();
    }

    [Fact]
    public async Task TR_13_Withdrawn_claim_is_read_only()
    {
        var created = await _handler.CreateOpenClaimAsync();
        (await ClaimsApi.ErrorsAsync(await _handler.TransitionAsync(created.Id, "Withdrawn")))["Reason"]
            .ShouldBe(["A reason is required to move a claim to Withdrawn."]);
        await _handler.TransitionOkAsync(created.Id, "Withdrawn", reason: "Claimant withdrew");

        var response = await _handler.Client.PostAsJsonAsync($"/api/claims/{created.Id}/parties", ClaimsApi.ClaimantPerson("Witness"));

        (await ClaimsApi.ErrorsAsync(response))["Claim"].ShouldBe(["Claim is Withdrawn; no changes are permitted."]);
    }

    [Fact]
    public async Task API_04_Unknown_claim_returns_404()
    {
        var response = await _handler.TransitionAsync(Guid.NewGuid(), "Open");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound");
    }
}
