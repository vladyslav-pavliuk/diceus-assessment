using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Claims.Events;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Reserves;
using static ClaimsModule.Domain.Tests.TestData;

namespace ClaimsModule.Domain.Tests.Reserves;

/// <summary>BR-R-05 / D-11: approved cost reserves may total at most $10,000,000 unless a manager sets the override.</summary>
public sealed class AggregateLimitTests
{
    [Fact]
    public void BR_R_05_Exactly_10M_is_allowed()
    {
        var claim = ClaimWithApprovedIndemnity(9_995_000m);

        var result = claim.SubmitReserveTransaction(ReserveComponentType.Expense, null, 5_000m, "Fees", Handler, Now);

        result.Transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved);
        result.Warnings.ShouldBeEmpty();
        claim.ApprovedAggregate.ShouldBe(10_000_000m);
    }

    [Fact]
    public void BR_R_05_Crossing_10M_without_override_warns_and_escalates_to_manager()
    {
        var claim = ClaimWithApprovedIndemnity(9_995_000m);

        // $5,000.01 is auto-approvable on its own (BR-R-02), but it would take the total over the limit.
        var result = claim.SubmitReserveTransaction(ReserveComponentType.Expense, null, 5_000.01m, "Fees", Handler, Now);

        result.Warnings.ShouldBe(["Total reserves will exceed $10,000,000. Manager override required."]); // VAL-10
        result.Transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        result.Transaction.RequiredAuthority.ShouldBe(ApprovalAuthority.Manager);
        result.Transaction.ExceedsAggregateLimit.ShouldBeTrue();
        claim.DomainEvents.OfType<ReserveAutoApproved>().ShouldBeEmpty();
    }

    [Fact]
    public void BR_R_05_Approval_blocked_until_override_set()
    {
        var claim = ClaimWithApprovedIndemnity(9_995_000m);
        var crossing = Submit(claim, ReserveComponentType.Expense, 20_000m);

        ShouldViolate(() => claim.ApproveReserveTransaction(crossing.Id, OtherManager, Now))[ErrorKeys.ReserveAmount]
            .ShouldBe([DomainMessages.AggregateLimitExceeded]);

        claim.SetReserveLimitOverride(true, "Catastrophic loss; board approval ref. 2026-114", Manager, Now);
        claim.ApproveReserveTransaction(crossing.Id, OtherManager, Now);

        claim.ApprovedAggregate.ShouldBe(10_015_000m);
        claim.ReserveLimitOverrideByUserId.ShouldBe(Manager.UserId);
        claim.SingleEvent<ReserveLimitOverrideSet>().Enabled.ShouldBeTrue();
    }

    [Fact]
    public void BR_R_05_Override_set_before_submission_keeps_the_normal_tier()
    {
        var claim = ClaimWithApprovedIndemnity(9_995_000m);
        claim.SetReserveLimitOverride(true, "Approved by the board", Manager, Now);

        var result = claim.SubmitReserveTransaction(ReserveComponentType.Expense, null, 8_000m, "Fees", Handler, Now);

        result.Transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved);
        result.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void BR_R_05_Limit_is_rechecked_at_approval()
    {
        // Two changes, each within the limit when submitted; approving both would exceed it.
        var claim = ClaimWithApprovedIndemnity(9_900_000m);
        var expense = Submit(claim, ReserveComponentType.Expense, 60_000m);
        var alae = Submit(claim, ReserveComponentType.ALAE, 60_000m);
        claim.ApproveReserveTransaction(expense.Id, Supervisor, Now);

        ShouldViolate(() => claim.ApproveReserveTransaction(alae.Id, Supervisor, Now))[ErrorKeys.ReserveAmount]
            .ShouldBe([DomainMessages.AggregateLimitExceeded]);
    }

    [Fact]
    public void BR_R_05_Subrogation_excluded_from_aggregate()
    {
        var claim = ClaimWithApprovedIndemnity(10_000_000m);
        Submit(claim, ReserveComponentType.SubrogationRecoverable, -5_000m);

        // The expected recovery does not create headroom (D-11) ...
        claim.ApprovedAggregate.ShouldBe(10_000_000m);
        Submit(claim, ReserveComponentType.Expense, 1m).ExceedsAggregateLimit.ShouldBeTrue();

        // ... and a subrogation change never triggers the limit.
        Submit(claim, ReserveComponentType.SubrogationRecoverable, 9_000m).ExceedsAggregateLimit.ShouldBeFalse();
    }

    [Fact]
    public void BR_R_05_A_decrease_is_never_blocked_by_the_limit()
    {
        var claim = ClaimWithApprovedIndemnity(10_000_000m);

        Submit(claim, ReserveComponentType.Indemnity, -5_000m).ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved);
    }

    [Fact]
    public void BR_R_05_Only_a_manager_sets_the_override_and_a_reason_is_required()
    {
        var claim = OpenClaim();

        Should.Throw<ForbiddenAccessException>(() => claim.SetReserveLimitOverride(true, "Reason", Supervisor, Now));
        ShouldViolate(() => claim.SetReserveLimitOverride(true, " ", Manager, Now))[ErrorKeys.ReserveLimitOverride]
            .ShouldBe(["A reason is required to change the reserve limit override."]);
    }

    private static Claim ClaimWithApprovedIndemnity(decimal amount)
    {
        var claim = OpenClaim();
        var indemnity = Submit(claim, ReserveComponentType.Indemnity, amount, submitter: Manager);
        claim.ApproveReserveTransaction(indemnity.Id, OtherManager, Now);
        claim.ClearDomainEvents();
        return claim;
    }
}
