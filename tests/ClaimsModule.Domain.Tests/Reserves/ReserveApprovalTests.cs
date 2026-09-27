using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Claims.Events;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Reserves;
using static ClaimsModule.Domain.Tests.TestData;

namespace ClaimsModule.Domain.Tests.Reserves;

/// <summary>FRS §6.4 approve / reject / retract, BR-R-03, BR-R-04.</summary>
public sealed class ReserveApprovalTests
{
    [Fact]
    public void BR_R_03_Self_approval_is_rejected()
    {
        var claim = OpenClaim();
        var transaction = Submit(claim, ReserveComponentType.Indemnity, 50_000m, submitter: Supervisor);

        ShouldViolate(() => claim.ApproveReserveTransaction(transaction.Id, Supervisor, Now))[ErrorKeys.ReserveApproval]
            .ShouldBe(["Self-approval is not permitted."]); // VAL-14, even though the role has the authority
        transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
    }

    [Fact]
    public void BR_R_03_Another_user_with_the_same_role_can_approve()
    {
        var claim = OpenClaim();
        var transaction = Submit(claim, ReserveComponentType.Indemnity, 150_000m, submitter: Manager);

        claim.ApproveReserveTransaction(transaction.Id, OtherManager, Now);

        transaction.ApprovedByUserId.ShouldBe(OtherManager.UserId);
    }

    [Fact]
    public void BR_R_03_Supervisor_self_approving_above_100000_gets_both_reasons()
    {
        var claim = OpenClaim();
        var transaction = Submit(claim, ReserveComponentType.Indemnity, 150_000m, submitter: Supervisor);

        ShouldViolate(() => claim.ApproveReserveTransaction(transaction.Id, Supervisor, Now))[ErrorKeys.ReserveApproval]
            .ShouldBe([DomainMessages.SelfApprovalNotPermitted, DomainMessages.NoApprovalAuthority]);
    }

    [Fact]
    public void RSV_03_Approve_sets_approved_and_raises_event()
    {
        var claim = OpenClaim();
        var transaction = Submit(claim, ReserveComponentType.Indemnity, 60_000m);
        claim.ClearDomainEvents();

        claim.ApproveReserveTransaction(transaction.Id, Supervisor, Now);

        transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.Approved);
        transaction.ApprovedByUserId.ShouldBe(Supervisor.UserId);
        transaction.ApprovedAt.ShouldBe(Now);
        claim.ReserveComponents.Single().CurrentAmount.ShouldBe(60_000m);
        claim.SingleEvent<ReserveApproved>().ShouldBe(new ReserveApproved(claim.Id, transaction.Id, transaction.IdempotencyKey, 60_000m));
    }

    [Fact]
    public void RSV_03_Only_a_pending_transaction_can_be_decided()
    {
        var claim = OpenClaim();
        var autoApproved = Submit(claim, ReserveComponentType.Indemnity, 1_000m);

        ShouldViolate(() => claim.ApproveReserveTransaction(autoApproved.Id, Supervisor, Now))[ErrorKeys.ReserveApproval]
            .ShouldBe(["Only a transaction pending approval can be approved, rejected or retracted."]);
    }

    [Fact]
    public void RSV_03_Unknown_transaction_is_not_found()
    {
        Should.Throw<NotFoundException>(() => OpenClaim().ApproveReserveTransaction(Guid.NewGuid(), Supervisor, Now));
    }

    [Fact]
    public void RSV_03_Reject_requires_reason()
    {
        var claim = OpenClaim();
        var transaction = Submit(claim, ReserveComponentType.Indemnity, 60_000m);

        ShouldViolate(() => claim.RejectReserveTransaction(transaction.Id, "  ", Supervisor, Now))[ErrorKeys.RejectionReason]
            .ShouldBe(["A rejection reason is required."]);
    }

    [Fact]
    public void RSV_03_Reject_needs_the_same_authority_as_approve()
    {
        var claim = OpenClaim();
        var transaction = Submit(claim, ReserveComponentType.Indemnity, 150_000m);

        ShouldViolate(() => claim.RejectReserveTransaction(transaction.Id, "Too high", Supervisor, Now))[ErrorKeys.ReserveApproval]
            .ShouldBe(["Your role does not have authority to reject this reserve amount."]);
    }

    [Fact]
    public void BR_R_04_Rejected_txn_is_retained_and_resubmission_creates_new_txn()
    {
        var claim = OpenClaim();
        var rejected = Submit(claim, ReserveComponentType.Indemnity, 60_000m);
        claim.RejectReserveTransaction(rejected.Id, "Estimate not supported", Supervisor, Now);

        var resubmitted = Submit(claim, ReserveComponentType.Indemnity, 45_000m);

        rejected.ApprovalStatus.ShouldBe(ReserveApprovalStatus.Rejected);
        rejected.RejectionReason.ShouldBe("Estimate not supported");
        rejected.RejectedByUserId.ShouldBe(Supervisor.UserId);
        rejected.RejectedAt.ShouldBe(Now);
        rejected.PostingStatus.ShouldBe(PostingStatus.Cancelled);
        resubmitted.Id.ShouldNotBe(rejected.Id);
        claim.ReserveComponents.Single().Transactions.ShouldBe([rejected, resubmitted]);
        claim.ReserveComponents.Single().CurrentAmount.ShouldBe(0m);
        claim.DomainEvents.OfType<ReserveRejected>().ShouldHaveSingleItem().Reason.ShouldBe("Estimate not supported");
    }

    [Fact]
    public void RSV_02_Retract_by_submitter_cancels_txn()
    {
        var claim = OpenClaim();
        var transaction = Submit(claim, ReserveComponentType.Indemnity, 60_000m);

        claim.RetractReserveTransaction(transaction.Id, Handler);

        transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.Cancelled);
        transaction.PostingStatus.ShouldBe(PostingStatus.Cancelled);
        claim.SingleEvent<ReserveRetracted>().TransactionId.ShouldBe(transaction.Id);
    }

    [Fact]
    public void RSV_02_Retract_by_other_user_is_rejected()
    {
        var claim = OpenClaim();
        var transaction = Submit(claim, ReserveComponentType.Indemnity, 60_000m);

        ShouldViolate(() => claim.RetractReserveTransaction(transaction.Id, Manager))[ErrorKeys.ReserveRetraction]
            .ShouldBe(["Only the submitter may retract a pending reserve."]);
    }

    [Fact]
    public void RSV_02_Retract_non_pending_is_rejected()
    {
        var claim = OpenClaim();
        var transaction = Submit(claim, ReserveComponentType.Indemnity, 60_000m);
        claim.ApproveReserveTransaction(transaction.Id, Supervisor, Now);

        ShouldViolate(() => claim.RetractReserveTransaction(transaction.Id, Handler))
            .ShouldContainKey(ErrorKeys.ReserveApproval);
    }
}
