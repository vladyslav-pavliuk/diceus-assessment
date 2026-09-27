using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Claims.Events;
using ClaimsModule.Domain.Reserves;
using static ClaimsModule.Domain.Tests.TestData;

namespace ClaimsModule.Domain.Tests.Reserves;

public sealed class ReserveSubmissionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BR_R_01_Opening_a_cost_reserve_needs_a_positive_amount(int amount)
    {
        ShouldViolate(() => Submit(OpenClaim(), ReserveComponentType.Indemnity, amount))[ErrorKeys.ReserveAmount]
            .ShouldBe(["Reserve amount must be greater than zero."]); // VAL-08
    }

    [Fact]
    public void BR_R_01_Missing_amount_is_rejected()
    {
        ShouldViolate(() => Submit(OpenClaim(), ReserveComponentType.Expense, null))[ErrorKeys.ReserveAmount]
            .ShouldBe([DomainMessages.ReserveAmountNotPositive]);
    }

    [Fact]
    public void BR_R_01_Negative_subrogation_is_accepted()
    {
        var claim = OpenClaim();

        var transaction = Submit(claim, ReserveComponentType.SubrogationRecoverable, -8_000m);

        transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved);
        claim.ReserveComponents.Single().CurrentAmount.ShouldBe(-8_000m);
    }

    [Fact]
    public void BR_R_01_Zero_subrogation_is_rejected()
    {
        ShouldViolate(() => Submit(OpenClaim(), ReserveComponentType.SubrogationRecoverable, 0m))[ErrorKeys.ReserveAmount]
            .ShouldBe(["Reserve amount must not be zero."]);
    }

    [Fact]
    public void BR_R_01_Subrogation_may_go_further_negative()
    {
        var claim = OpenClaim();
        Submit(claim, ReserveComponentType.SubrogationRecoverable, 2_000m);

        Submit(claim, ReserveComponentType.SubrogationRecoverable, -9_000m);

        claim.ReserveComponents.Single().CurrentAmount.ShouldBe(-7_000m);
    }

    [Fact]
    public void BR_R_01_Adjust_below_zero_balance_is_rejected()
    {
        var claim = OpenClaim();
        Submit(claim, ReserveComponentType.Indemnity, 5_000m);

        ShouldViolate(() => Submit(claim, ReserveComponentType.Indemnity, -5_000.01m))[ErrorKeys.ReserveAmount]
            .ShouldBe(["Reserve balance for Indemnity cannot go below zero."]);
    }

    [Fact]
    public void BR_R_01_Adjust_down_to_exactly_zero_is_allowed()
    {
        var claim = OpenClaim();
        Submit(claim, ReserveComponentType.Expense, 5_000m);

        Submit(claim, ReserveComponentType.Expense, -5_000m).NewBalance.ShouldBe(0m);
    }

    [Fact]
    public void BR_R_01_Zero_adjustment_is_rejected()
    {
        var claim = OpenClaim();
        Submit(claim, ReserveComponentType.Expense, 5_000m);

        ShouldViolate(() => Submit(claim, ReserveComponentType.Expense, 0m))[ErrorKeys.ReserveAmount]
            .ShouldBe(["Adjustment amount must not be zero."]);
    }

    [Fact]
    public void BR_R_01_Reverse_zeroes_component()
    {
        var claim = OpenClaim();
        Submit(claim, ReserveComponentType.ALAE, 7_500m);

        var reversal = Submit(claim, ReserveComponentType.ALAE, null, type: ReserveTransactionType.Reverse);

        reversal.TransactionType.ShouldBe(ReserveTransactionType.Reverse);
        reversal.Amount.ShouldBe(-7_500m);
        reversal.NewBalance.ShouldBe(0m);
        claim.ReserveComponents.Single().CurrentAmount.ShouldBe(0m);
    }

    [Fact]
    public void BR_R_01_Reverse_rejects_a_client_amount_and_a_zero_balance()
    {
        var claim = OpenClaim();
        Submit(claim, ReserveComponentType.ALAE, 7_500m);

        ShouldViolate(() => Submit(claim, ReserveComponentType.ALAE, -7_500m, type: ReserveTransactionType.Reverse))[ErrorKeys.ReserveAmount]
            .ShouldBe(["The amount of a Reverse transaction is computed by the system; omit it."]);

        Submit(claim, ReserveComponentType.ALAE, null, type: ReserveTransactionType.Reverse);
        ShouldViolate(() => Submit(claim, ReserveComponentType.ALAE, null, type: ReserveTransactionType.Reverse))[ErrorKeys.ReserveAmount]
            .ShouldBe(["Reserve balance for ALAE is already zero."]);
    }

    [Fact]
    public void D_05_Transaction_type_is_inferred_from_the_component()
    {
        var claim = OpenClaim();

        Submit(claim, ReserveComponentType.Indemnity, 1_000m).TransactionType.ShouldBe(ReserveTransactionType.Add);
        Submit(claim, ReserveComponentType.Indemnity, 1_000m).TransactionType.ShouldBe(ReserveTransactionType.Adjust);
    }

    [Fact]
    public void D_05_Add_on_an_existing_component_and_adjust_on_a_missing_one_are_rejected()
    {
        var claim = OpenClaim();
        Submit(claim, ReserveComponentType.Indemnity, 1_000m);

        ShouldViolate(() => Submit(claim, ReserveComponentType.Indemnity, 1_000m, type: ReserveTransactionType.Add))[ErrorKeys.TransactionType]
            .ShouldBe(["A Indemnity reserve already exists on this claim; submit an Adjust transaction."]);
        ShouldViolate(() => Submit(claim, ReserveComponentType.Expense, 1_000m, type: ReserveTransactionType.Adjust))[ErrorKeys.TransactionType]
            .ShouldBe(["There is no Expense reserve on this claim to adjust; submit an Add transaction."]);
    }

    [Fact]
    public void VAL_09_Invalid_component_is_rejected()
    {
        ShouldViolate(() => Submit(OpenClaim(), (ReserveComponentType)99, 1_000m))[ErrorKeys.ReserveComponent]
            .ShouldBe(["Invalid reserve component type."]);
    }

    [Fact]
    public void BR_C_06_Reserve_on_claim_without_policy_is_rejected()
    {
        var claim = OpenClaim(withPolicy: false);

        ShouldViolate(() => Submit(claim, ReserveComponentType.Indemnity, 1_000m))[ErrorKeys.PolicyId]
            .ShouldBe(["No policy linked. Policy must be associated before reserves can be set."]);
        claim.ReserveComponents.ShouldBeEmpty();
    }

    [Fact]
    public void RSV_01_Claim_can_hold_multiple_components()
    {
        var claim = OpenClaim();

        Submit(claim, ReserveComponentType.Indemnity, 9_000m);
        Submit(claim, ReserveComponentType.Expense, 5_000m);
        Submit(claim, ReserveComponentType.SubrogationRecoverable, -8_000m);

        claim.ReserveComponents.Select(component => (component.Component, component.CurrentAmount)).ShouldBe(
        [
            (ReserveComponentType.Indemnity, 9_000m),
            (ReserveComponentType.Expense, 5_000m),
            (ReserveComponentType.SubrogationRecoverable, -8_000m),
        ]);
    }

    [Fact]
    public void RSV_02_Pending_transaction_cannot_be_modified()
    {
        var claim = OpenClaim();
        var pending = Submit(claim, ReserveComponentType.Indemnity, 40_000m);

        // "If the submitter wishes to change the amount before approval, they must first retract" (FRS §6.4).
        ShouldViolate(() => Submit(claim, ReserveComponentType.Indemnity, 35_000m))[ErrorKeys.ReserveComponent]
            .ShouldBe(["Component has a pending transaction; retract it or wait for a decision."]);
        pending.Amount.ShouldBe(40_000m);
        typeof(ReserveTransaction).GetProperty(nameof(ReserveTransaction.Amount))!.SetMethod!.IsPublic.ShouldBeFalse();
    }

    [Fact]
    public void RSV_06_Even_an_auto_approvable_change_waits_for_the_pending_one()
    {
        var claim = OpenClaim();
        Submit(claim, ReserveComponentType.Indemnity, 40_000m);

        ShouldViolate(() => Submit(claim, ReserveComponentType.Indemnity, 500m)).ShouldContainKey(ErrorKeys.ReserveComponent);
    }

    [Fact]
    public void RSV_06_A_pending_transaction_does_not_block_other_components()
    {
        var claim = OpenClaim();
        Submit(claim, ReserveComponentType.Indemnity, 40_000m);

        Submit(claim, ReserveComponentType.Expense, 500m).ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved);
    }

    [Fact]
    public void RSV_02_After_retraction_a_new_transaction_can_be_submitted()
    {
        var claim = OpenClaim();
        var pending = Submit(claim, ReserveComponentType.Indemnity, 40_000m);
        claim.RetractReserveTransaction(pending.Id, Handler);

        var replacement = Submit(claim, ReserveComponentType.Indemnity, 35_000m);

        replacement.TransactionType.ShouldBe(ReserveTransactionType.Adjust);
        replacement.PreviousBalance.ShouldBe(0m);
        replacement.ChangeSequence.ShouldBe(2);
    }

    [Fact]
    public void RSV_04_Current_amount_is_sum_of_approved_txns()
    {
        var claim = OpenClaim();
        Submit(claim, ReserveComponentType.Indemnity, 5_000m);                          // auto-approved   +5,000
        var approved = Submit(claim, ReserveComponentType.Indemnity, 20_000m);          // approved       +20,000
        claim.ApproveReserveTransaction(approved.Id, Supervisor, Now);
        var rejected = Submit(claim, ReserveComponentType.Indemnity, 30_000m);          // rejected: no effect
        claim.RejectReserveTransaction(rejected.Id, "Not supported by the estimate", Supervisor, Now);
        var retracted = Submit(claim, ReserveComponentType.Indemnity, 15_000m);         // retracted: no effect
        claim.RetractReserveTransaction(retracted.Id, Handler);
        Submit(claim, ReserveComponentType.Indemnity, -2_500m);                         // auto-approved   -2,500

        var component = claim.ReserveComponents.Single();
        component.Transactions.Count.ShouldBe(5);
        component.CurrentAmount.ShouldBe(22_500m);
        component.CurrentAmount.ShouldBe(component.Transactions.Where(t => t.IsApproved).Sum(t => t.Amount));
    }

    [Fact]
    public void RSV_05_Change_sequence_increments_per_component_and_is_never_reused()
    {
        var claim = OpenClaim();
        var first = Submit(claim, ReserveComponentType.Indemnity, 1_000m);
        var rejected = Submit(claim, ReserveComponentType.Indemnity, 50_000m);
        claim.RejectReserveTransaction(rejected.Id, "Too high", Supervisor, Now);
        var third = Submit(claim, ReserveComponentType.Indemnity, 1_000m);
        var otherComponent = Submit(claim, ReserveComponentType.Expense, 1_000m);

        (first.ChangeSequence, rejected.ChangeSequence, third.ChangeSequence).ShouldBe((1, 2, 3));
        otherComponent.ChangeSequence.ShouldBe(1);
        claim.ReserveComponents.Single(c => c.Component == ReserveComponentType.Indemnity).LastChangeSequence.ShouldBe(3);
    }

    [Fact]
    public void BR_R_06_Idempotency_key_format()
    {
        var claim = OpenClaim();
        Submit(claim, ReserveComponentType.Indemnity, 1_000m);

        var second = Submit(claim, ReserveComponentType.Indemnity, 1_000m);

        var componentId = claim.ReserveComponents.Single().Id;
        second.IdempotencyKey.ShouldBe($"Reserve:{componentId}:Change:2");
        GlIdempotencyKey.For(componentId, 2).ShouldBe(second.IdempotencyKey);
    }

    [Fact]
    public void RSV_08_History_row_captures_balances_and_actors()
    {
        var claim = OpenClaim();
        Submit(claim, ReserveComponentType.Indemnity, 4_000m);

        var result = claim.SubmitReserveTransaction(ReserveComponentType.Indemnity, null, 26_000m, "  Surveyor report received  ", OtherHandler, Now);
        claim.ApproveReserveTransaction(result.Transaction.Id, Supervisor, Now.AddHours(1));

        var row = result.Transaction;
        row.ClaimId.ShouldBe(claim.Id);
        row.ReserveComponentId.ShouldBe(claim.ReserveComponents.Single().Id);
        (row.PreviousBalance, row.Amount, row.NewBalance).ShouldBe((4_000m, 26_000m, 30_000m));
        row.ChangeReason.ShouldBe("Surveyor report received");
        row.SubmittedByUserId.ShouldBe(OtherHandler.UserId);
        row.ApprovedByUserId.ShouldBe(Supervisor.UserId);
        row.ApprovedAt.ShouldBe(Now.AddHours(1));
        row.PostingStatus.ShouldBe(PostingStatus.Pending);
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void RSV_08_Submission_raises_an_event_describing_the_transaction()
    {
        var claim = OpenClaim();

        var transaction = Submit(claim, ReserveComponentType.Expense, 12_000m);

        claim.SingleEvent<ReserveTransactionSubmitted>().ShouldBe(new ReserveTransactionSubmitted(
            claim.Id, transaction.Id, transaction.ReserveComponentId, ReserveComponentType.Expense, ReserveTransactionType.Add,
            12_000m, 0m, 12_000m, ReserveApprovalStatus.PendingApproval, ApprovalAuthority.Supervisor, ExceedsAggregateLimit: false));
    }

    [Fact]
    public void FRS_15_1_Amounts_with_more_than_four_decimals_are_rejected()
    {
        ShouldViolate(() => Submit(OpenClaim(), ReserveComponentType.Expense, 10.00001m))[ErrorKeys.ReserveAmount]
            .ShouldBe(["Reserve amount must have at most 4 decimal places."]);
    }

    [Fact]
    public void FRS_9_6_A_change_reason_is_required()
    {
        ShouldViolate(() => OpenClaim().SubmitReserveTransaction(ReserveComponentType.Expense, null, 100m, " ", Handler, Now))[ErrorKeys.ChangeReason]
            .ShouldBe(["A change reason is required."]);
    }

    [Fact]
    public void FRS_5_2_A_draft_claim_can_take_an_initial_reserve()
    {
        var claim = DraftClaim();

        Submit(claim, ReserveComponentType.Indemnity, 2_000m).ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved);
    }
}
