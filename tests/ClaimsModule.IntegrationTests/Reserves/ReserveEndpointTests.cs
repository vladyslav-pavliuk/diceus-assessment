using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Claims;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.IntegrationTests.Fixtures;

namespace ClaimsModule.IntegrationTests.Reserves;

/// <summary>
/// The reserve endpoints through HTTP (FRS §6, §7.2, §10.2; brief §3.3.3; D-04, D-05, D-11, D-25). Messages are
/// asserted word for word; the GL job is checked in the real Hangfire storage (no server runs in tests).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReserveEndpointTests(ApiFixture fixture) : IAsyncLifetime
{
    private ClaimsApi _handler = null!;
    private ClaimsApi _otherHandler = null!;
    private ClaimsApi _supervisor = null!;
    private ClaimsApi _otherSupervisor = null!;
    private ClaimsApi _manager = null!;

    private IServiceProvider Services => fixture.Factory.Services;

    public async Task InitializeAsync()
    {
        _handler = await ClaimsApi.SignInAsync(fixture.Factory, "handler.alex");
        _otherHandler = await ClaimsApi.SignInAsync(fixture.Factory, "handler.blake");
        _supervisor = await ClaimsApi.SignInAsync(fixture.Factory, "supervisor.casey");
        _otherSupervisor = await ClaimsApi.SignInAsync(fixture.Factory, "supervisor.drew");
        _manager = await ClaimsApi.SignInAsync(fixture.Factory, "manager.emery");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task API_10_Submit_auto_approved_reserve()
    {
        var claim = await _handler.CreateClaimAsync();

        var submitted = await _handler.SubmitReserveAsync(claim.Id, "Indemnity", 10_000m);

        submitted.Component.ShouldBe(ReserveComponentType.Indemnity);
        submitted.Warnings.ShouldBeEmpty();
        var transaction = submitted.Transaction;
        transaction.TransactionType.ShouldBe(ReserveTransactionType.Add); // inferred (D-05)
        transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved); // BR-R-02: exactly $10,000
        transaction.PostingStatus.ShouldBe(PostingStatus.Pending);
        transaction.IdempotencyKey.ShouldBe($"Reserve:{transaction.ReserveComponentId}:Change:1"); // BR-R-06

        var audit = await _handler.AuditAsync(claim.Id);
        audit.Select(entry => entry.EventType).ShouldContain(AuditEventTypes.ReserveCreated); // AUD-05
        audit.Select(entry => entry.EventType).ShouldContain(AuditEventTypes.ReserveAutoApproved); // AUD-06

        // JOB-01: enqueued once, after commit, with the three FRS §12.1 arguments.
        var job = HangfireJobs.EnqueuedGlPostings(Services, transaction.Id).ShouldHaveSingleItem();
        job.Args[1].ShouldBe(claim.Id);
        job.Args[2].ShouldBe(transaction.IdempotencyKey);
    }

    [Fact]
    public async Task API_10_Submit_pending_reserve()
    {
        var claim = await _handler.CreateClaimAsync();

        var transaction = (await _handler.SubmitReserveAsync(claim.Id, "Expense", 10_000.01m)).Transaction;

        transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        transaction.RequiredAuthority.ShouldBe(ApprovalAuthority.Supervisor);
        HangfireJobs.EnqueuedGlPostings(Services, transaction.Id).ShouldBeEmpty(); // BR_R_02: no GL job until approved
        (await _handler.GetReservesAsync(claim.Id)).Components.Single().PendingAmount.ShouldBe(10_000.01m);
    }

    [Fact]
    public async Task VAL_09_Invalid_reserve_component_returns_422()
    {
        var claim = await _handler.CreateClaimAsync();

        var response = await _handler.PostReserveAsync(claim.Id, new { component = "Litigation", amount = 100m, changeReason = "Estimate." });

        (await ClaimsApi.ErrorsAsync(response))["ReserveComponent"].ShouldBe(["Invalid reserve component type."]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task VAL_08_Reserve_amount_not_above_zero_returns_422(decimal amount)
    {
        var claim = await _handler.CreateClaimAsync();

        // Explicit Add: the validator applies BR-R-01; omitted type on a new component: the aggregate does.
        var explicitAdd = await _handler.PostReserveAsync(claim.Id, new { component = "Indemnity", amount, changeReason = "Estimate.", transactionType = "Add" });
        var inferred = await _handler.PostReserveAsync(claim.Id, new { component = "Indemnity", amount, changeReason = "Estimate." });

        (await ClaimsApi.ErrorsAsync(explicitAdd))["ReserveAmount"].ShouldBe(["Reserve amount must be greater than zero."]);
        (await ClaimsApi.ErrorsAsync(inferred))["ReserveAmount"].ShouldBe(["Reserve amount must be greater than zero."]);
    }

    [Fact]
    public async Task BR_R_01_Negative_subrogation_reserve_is_accepted_over_http()
    {
        var claim = await _handler.CreateClaimAsync();

        var submitted = await _handler.SubmitReserveAsync(claim.Id, "SubrogationRecoverable", -8_000m);

        submitted.Transaction.Amount.ShouldBe(-8_000m);
        submitted.Transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved); // |−8,000| ≤ 10,000 (D-05)
    }

    [Fact]
    public async Task API_10_Change_reason_is_required()
    {
        var claim = await _handler.CreateClaimAsync();

        var response = await _handler.PostReserveAsync(claim.Id, new { component = "Indemnity", amount = 100m });

        (await ClaimsApi.ErrorsAsync(response))["ChangeReason"].ShouldBe(["A change reason is required."]);
    }

    [Fact]
    public async Task BR_C_06_Reserve_on_a_claim_without_a_policy_returns_422()
    {
        var claim = await _handler.CreateClaimAsync(ClaimsApi.FnolBody(policyId: null));

        var response = await _handler.PostReserveAsync(claim.Id, new { component = "Indemnity", amount = 100m, changeReason = "Estimate." });

        (await ClaimsApi.ErrorsAsync(response))["PolicyId"].ShouldBe(["No policy linked. Policy must be associated before reserves can be set."]);
    }

    [Fact]
    public async Task API_11_Put_reserve_creates_adjust_txn_with_delta()
    {
        var claim = await _handler.CreateClaimAsync();
        var opened = (await _handler.SubmitReserveAsync(claim.Id, "Indemnity", 5_000m)).Transaction;

        var response = await _handler.Client.PutAsJsonAsync(
            $"/api/claims/{claim.Id}/reserves/{opened.ReserveComponentId}", new { newAmount = 8_000m, changeReason = "Repair quote received." });

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var adjust = (await response.Content.ReadFromJsonAsync<ReserveSubmittedDto>(TestAuth.Json))!.Transaction;
        adjust.TransactionType.ShouldBe(ReserveTransactionType.Adjust);
        adjust.Amount.ShouldBe(3_000m); // D-04: delta = newAmount − CurrentAmount
        adjust.PreviousBalance.ShouldBe(5_000m);
        adjust.NewBalance.ShouldBe(8_000m);
        adjust.ChangeSequence.ShouldBe(2);
        (await _handler.GetReservesAsync(claim.Id)).Components.Single().CurrentAmount.ShouldBe(8_000m);

        var unchanged = await _handler.Client.PutAsJsonAsync(
            $"/api/claims/{claim.Id}/reserves/{opened.ReserveComponentId}", new { newAmount = 8_000m, changeReason = "No change." });
        (await ClaimsApi.ErrorsAsync(unchanged))["ReserveAmount"].ShouldBe(["Adjustment amount must not be zero."]);

        var unknown = await _handler.Client.PutAsJsonAsync(
            $"/api/claims/{claim.Id}/reserves/{Guid.NewGuid()}", new { newAmount = 1m, changeReason = "Estimate." });
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task API_12_Get_reserves_summary_and_history()
    {
        var claim = await _handler.CreateClaimAsync();
        var indemnity = (await _handler.SubmitReserveAsync(claim.Id, "Indemnity", 25_000m)).Transaction;
        await _supervisor.DecideOkAsync(claim.Id, indemnity.Id, "approve");
        await _handler.SubmitReserveAsync(claim.Id, "Expense", 5_000m);
        await _handler.SubmitReserveAsync(claim.Id, "SubrogationRecoverable", -8_000m);
        var pending = (await _handler.SubmitReserveAsync(claim.Id, "Indemnity", 20_000m)).Transaction;

        var reserves = await _handler.GetReservesAsync(claim.Id);

        // FRS §6.2 example: Indemnity 25,000, Expense 5,000, SubrogationRecoverable −8,000.
        reserves.Components.Select(component => (component.Component, component.CurrentAmount, component.PendingAmount)).ShouldBe(
        [
            (ReserveComponentType.Indemnity, 25_000m, 20_000m),
            (ReserveComponentType.Expense, 5_000m, 0m),
            (ReserveComponentType.SubrogationRecoverable, -8_000m, 0m),
        ]);
        reserves.TotalReserves.ShouldBe(22_000m); // net (D-29)
        reserves.ApprovedAggregate.ShouldBe(30_000m); // cost components only (D-11)
        reserves.AggregateLimit.ShouldBe(10_000_000m);

        reserves.Transactions.Count.ShouldBe(4);
        reserves.Transactions[0].Id.ShouldBe(pending.Id); // newest first
        var approved = reserves.Transactions.Single(transaction => transaction.Id == indemnity.Id);
        approved.ApprovalStatus.ShouldBe(ReserveApprovalStatus.Approved);
        approved.SubmittedByName.ShouldNotBeNullOrWhiteSpace();
        approved.ApprovedByName.ShouldNotBeNullOrWhiteSpace();
        approved.ApprovedByName.ShouldNotBe(approved.SubmittedByName);
    }

    [Fact]
    public async Task API_12_Unknown_claim_returns_404()
    {
        (await _handler.Client.GetAsync($"/api/claims/{Guid.NewGuid()}/reserves")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BR_R_03_Self_approval_returns_422_with_exact_message()
    {
        var claim = await _supervisor.CreateClaimAsync();
        var transaction = (await _supervisor.SubmitReserveAsync(claim.Id, "Indemnity", 50_000m)).Transaction;

        var response = await _supervisor.DecideAsync(claim.Id, transaction.Id, "approve");

        (await ClaimsApi.ErrorsAsync(response))["ReserveApproval"].ShouldBe(["Self-approval is not permitted."]);
        HangfireJobs.EnqueuedGlPostings(Services, transaction.Id).ShouldBeEmpty();
    }

    [Fact]
    public async Task SEC_02_Handler_approve_returns_403()
    {
        var claim = await _handler.CreateClaimAsync();
        var transaction = (await _handler.SubmitReserveAsync(claim.Id, "Indemnity", 50_000m)).Transaction;

        var approve = await _otherHandler.DecideAsync(claim.Id, transaction.Id, "approve");
        var reject = await _otherHandler.DecideAsync(claim.Id, transaction.Id, "reject", new { rejectionReason = "No." });

        await approve.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden"); // D-25: the role can never approve
        await reject.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden");
    }

    [Fact]
    public async Task BR_R_02_Supervisor_approving_above_100000_returns_422()
    {
        var claim = await _handler.CreateClaimAsync();
        var transaction = (await _handler.SubmitReserveAsync(claim.Id, "Indemnity", 100_000.01m)).Transaction;
        transaction.RequiredAuthority.ShouldBe(ApprovalAuthority.Manager);

        var bySupervisor = await _supervisor.DecideAsync(claim.Id, transaction.Id, "approve");
        (await ClaimsApi.ErrorsAsync(bySupervisor))["ReserveApproval"]
            .ShouldBe(["Your role does not have authority to approve this reserve amount."]); // VAL-13, D-25: data-dependent → 422

        (await _manager.DecideOkAsync(claim.Id, transaction.Id, "approve")).ApprovalStatus.ShouldBe(ReserveApprovalStatus.Approved);
    }

    [Fact]
    public async Task RSV_03_Approval_enqueues_gl_job_after_commit()
    {
        var claim = await _handler.CreateClaimAsync();
        var transaction = (await _handler.SubmitReserveAsync(claim.Id, "Indemnity", 60_000m)).Transaction;

        var approved = await _supervisor.DecideOkAsync(claim.Id, transaction.Id, "approve");

        approved.ApprovalStatus.ShouldBe(ReserveApprovalStatus.Approved);
        approved.ApprovedByUserId.ShouldNotBeNull();
        approved.PostingStatus.ShouldBe(PostingStatus.Pending);
        var job = HangfireJobs.EnqueuedGlPostings(Services, transaction.Id).ShouldHaveSingleItem();
        job.Args[2].ShouldBe(transaction.IdempotencyKey);
        (await _handler.AuditAsync(claim.Id)).First().EventType.ShouldBe(AuditEventTypes.ReserveApproved); // AUD-07
        (await _handler.GetReservesAsync(claim.Id)).Components.Single().CurrentAmount.ShouldBe(60_000m);
    }

    [Fact]
    public async Task API_14_Reject_requires_reason()
    {
        var claim = await _handler.CreateClaimAsync();
        var transaction = (await _handler.SubmitReserveAsync(claim.Id, "Indemnity", 30_000m)).Transaction;

        var noReason = await _supervisor.DecideAsync(claim.Id, transaction.Id, "reject", new { rejectionReason = " " });
        (await ClaimsApi.ErrorsAsync(noReason))["RejectionReason"].ShouldBe(["A rejection reason is required."]);

        var rejected = await _supervisor.DecideOkAsync(claim.Id, transaction.Id, "reject", new { rejectionReason = "Estimate not supported by the survey." });
        rejected.ApprovalStatus.ShouldBe(ReserveApprovalStatus.Rejected);
        rejected.PostingStatus.ShouldBe(PostingStatus.Cancelled);
        rejected.RejectionReason.ShouldBe("Estimate not supported by the survey.");

        var audit = (await _handler.AuditAsync(claim.Id)).First();
        audit.EventType.ShouldBe(AuditEventTypes.ReserveRejected); // AUD-08
        audit.OldValue!.ShouldContain("Estimate not supported by the survey."); // FRS §14.1 "OldValue includes rejection reason"

        // BR-R-04: the rejected row stays; a resubmission is a new row.
        var resubmitted = (await _handler.SubmitReserveAsync(claim.Id, "Indemnity", 22_000m, "Revised estimate.")).Transaction;
        resubmitted.Id.ShouldNotBe(transaction.Id);
        resubmitted.ChangeSequence.ShouldBe(2);
        (await _handler.GetReservesAsync(claim.Id)).Transactions.Select(row => row.ApprovalStatus)
            .ShouldBe([ReserveApprovalStatus.PendingApproval, ReserveApprovalStatus.Rejected]);
    }

    [Fact]
    public async Task D_45_Self_rejection_returns_422_and_the_submitter_retracts_instead()
    {
        var claim = await _handler.CreateClaimAsync();
        var transaction = (await _supervisor.SubmitReserveAsync(claim.Id, "Indemnity", 30_000m)).Transaction;

        var selfReject = await _supervisor.DecideAsync(claim.Id, transaction.Id, "reject", new { rejectionReason = "Changed my mind." });
        (await ClaimsApi.ErrorsAsync(selfReject))["ReserveApproval"]
            .ShouldBe(["Self-rejection is not permitted. Use Retract to withdraw your own pending reserve."]);

        (await _supervisor.DecideOkAsync(claim.Id, transaction.Id, "retract")).ApprovalStatus.ShouldBe(ReserveApprovalStatus.Cancelled);
    }

    [Fact]
    public async Task RSV_02_Only_the_submitter_retracts_a_pending_reserve()
    {
        var claim = await _handler.CreateClaimAsync();
        var transaction = (await _handler.SubmitReserveAsync(claim.Id, "Indemnity", 30_000m)).Transaction;

        var byOther = await _otherHandler.DecideAsync(claim.Id, transaction.Id, "retract");
        (await ClaimsApi.ErrorsAsync(byOther))["ReserveRetraction"].ShouldBe(["Only the submitter may retract a pending reserve."]);

        var retracted = await _handler.DecideOkAsync(claim.Id, transaction.Id, "retract");
        retracted.ApprovalStatus.ShouldBe(ReserveApprovalStatus.Cancelled);
        retracted.PostingStatus.ShouldBe(PostingStatus.Cancelled);
        (await _handler.AuditAsync(claim.Id)).First().EventType.ShouldBe(AuditEventTypes.ReserveRetracted); // AUD-09

        var again = await _handler.DecideAsync(claim.Id, transaction.Id, "approve");
        again.StatusCode.ShouldBe(HttpStatusCode.Forbidden); // a handler still cannot approve anything
        var decideCancelled = await _supervisor.DecideAsync(claim.Id, transaction.Id, "approve");
        (await ClaimsApi.ErrorsAsync(decideCancelled))["ReserveApproval"]
            .ShouldBe(["Only a transaction pending approval can be approved, rejected or retracted."]);
    }

    [Fact]
    public async Task RSV_03_Unknown_transaction_returns_404()
    {
        var claim = await _handler.CreateClaimAsync();

        (await _supervisor.DecideAsync(claim.Id, Guid.NewGuid(), "approve")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task API_24_Only_manager_sets_override()
    {
        var claim = await _handler.CreateClaimAsync();

        var bySupervisor = await _supervisor.SetReserveLimitOverrideAsync(claim.Id, true, "Large loss.");
        await bySupervisor.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden");

        var noReason = await _manager.SetReserveLimitOverrideAsync(claim.Id, true, null);
        (await ClaimsApi.ErrorsAsync(noReason))["Reason"].ShouldBe(["A reason is required to change the reserve limit override."]);

        var byManager = await _manager.SetReserveLimitOverrideAsync(claim.Id, true, "Catastrophe claim; board approval ref 2026-114.");
        byManager.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detail = await _handler.GetDetailAsync(claim.Id);
        detail.ReserveLimitOverride.ShouldBeTrue();
        detail.ReserveLimitOverrideReason.ShouldBe("Catastrophe claim; board approval ref 2026-114.");
        (await _handler.AuditAsync(claim.Id)).First().EventType.ShouldBe(AuditEventTypes.ReserveLimitOverrideSet); // AUD-17
    }

    [Fact]
    public async Task BR_R_05_Crossing_10M_warns_escalates_and_waits_for_the_override()
    {
        var claim = await _handler.CreateClaimAsync();
        var large = (await _handler.SubmitReserveAsync(claim.Id, "Indemnity", 9_995_000m)).Transaction;
        await _manager.DecideOkAsync(claim.Id, large.Id, "approve");

        // $5,000.01 would auto-approve on its own, but it takes the approved total over $10,000,000 (D-11).
        var crossing = await _handler.SubmitReserveAsync(claim.Id, "Expense", 5_000.01m);

        crossing.Warnings.ShouldBe(["Total reserves will exceed $10,000,000. Manager override required."]); // VAL-10
        crossing.Transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        crossing.Transaction.RequiredAuthority.ShouldBe(ApprovalAuthority.Manager);
        crossing.Transaction.ExceedsAggregateLimit.ShouldBeTrue();

        var blocked = await _manager.DecideAsync(claim.Id, crossing.Transaction.Id, "approve");
        (await ClaimsApi.ErrorsAsync(blocked))["ReserveAmount"].ShouldBe(["Total reserves will exceed $10,000,000. Manager override required."]);

        (await _manager.SetReserveLimitOverrideAsync(claim.Id, true, "Confirmed large loss.")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _manager.DecideOkAsync(claim.Id, crossing.Transaction.Id, "approve")).ApprovalStatus.ShouldBe(ReserveApprovalStatus.Approved);
        (await _handler.GetReservesAsync(claim.Id)).ApprovedAggregate.ShouldBe(10_000_000.01m);
    }

    [Fact]
    public async Task D_26_Reserves_cannot_change_on_a_closed_claim()
    {
        var claim = await _handler.CreateOpenClaimAsync();
        await _handler.TransitionOkAsync(claim.Id, "Closed", reason: "Settled.");

        var response = await _handler.PostReserveAsync(claim.Id, new { component = "Indemnity", amount = 100m, changeReason = "Late estimate." });

        (await ClaimsApi.ErrorsAsync(response))["Claim"].ShouldBe(["Claim is Closed; no changes are permitted."]);
    }
}
