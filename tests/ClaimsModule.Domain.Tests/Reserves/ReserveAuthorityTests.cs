using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Claims.Events;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Domain.Users;
using static ClaimsModule.Domain.Tests.TestData;

namespace ClaimsModule.Domain.Tests.Reserves;

/// <summary>FRS §6.3 / BR-R-02: the tier follows the amount of the single transaction.</summary>
public sealed class ReserveAuthorityTests
{
    [Theory]
    [InlineData("0.0001", ApprovalAuthority.Auto)]
    [InlineData("10000", ApprovalAuthority.Auto)]
    [InlineData("10000.01", ApprovalAuthority.Supervisor)]
    [InlineData("100000", ApprovalAuthority.Supervisor)]
    [InlineData("100000.01", ApprovalAuthority.Manager)]
    [InlineData("-10000", ApprovalAuthority.Auto)]
    [InlineData("-10000.01", ApprovalAuthority.Supervisor)]
    [InlineData("-100000.01", ApprovalAuthority.Manager)]
    public void BR_R_02_Tier_boundaries(string amount, ApprovalAuthority expected)
    {
        ReserveAuthorityPolicy.RequiredAuthorityFor(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(UserRole.Handler, ApprovalAuthority.Auto, true)]
    [InlineData(UserRole.Handler, ApprovalAuthority.Supervisor, false)]
    [InlineData(UserRole.Handler, ApprovalAuthority.Manager, false)]
    [InlineData(UserRole.Supervisor, ApprovalAuthority.Supervisor, true)]
    [InlineData(UserRole.Supervisor, ApprovalAuthority.Manager, false)]
    [InlineData(UserRole.Manager, ApprovalAuthority.Supervisor, true)]
    [InlineData(UserRole.Manager, ApprovalAuthority.Manager, true)]
    public void BR_R_02_Who_can_approve_each_tier(UserRole role, ApprovalAuthority tier, bool expected)
    {
        ReserveAuthorityPolicy.CanApprove(role, tier).ShouldBe(expected);
    }

    [Fact]
    public void BR_R_02_Amount_10000_is_auto_approved()
    {
        var claim = OpenClaim();

        var transaction = Submit(claim, ReserveComponentType.Indemnity, 10_000m);

        transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved);
        transaction.RequiredAuthority.ShouldBe(ApprovalAuthority.Auto);
        transaction.ApprovedAt.ShouldBe(Now);
        transaction.ApprovedByUserId.ShouldBeNull();
        claim.ReserveComponents.Single().CurrentAmount.ShouldBe(10_000m);

        // Auto-approval is what enqueues the GL posting job (after commit).
        claim.SingleEvent<ReserveAutoApproved>().IdempotencyKey.ShouldBe(transaction.IdempotencyKey);
    }

    [Fact]
    public void BR_R_02_Amount_10000_01_requires_supervisor()
    {
        AssertPending(10_000.01m, ApprovalAuthority.Supervisor);
    }

    [Fact]
    public void BR_R_02_Amount_100000_requires_supervisor()
    {
        AssertPending(100_000m, ApprovalAuthority.Supervisor);
    }

    [Fact]
    public void BR_R_02_Amount_100000_01_requires_manager()
    {
        AssertPending(100_000.01m, ApprovalAuthority.Manager);
    }

    [Fact]
    public void BR_R_02_Negative_amount_uses_absolute_value()
    {
        var claim = OpenClaim();
        var opening = Submit(claim, ReserveComponentType.Indemnity, 90_000m);
        claim.ApproveReserveTransaction(opening.Id, Supervisor, Now);

        var release = Submit(claim, ReserveComponentType.Indemnity, -50_000m);

        release.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        release.RequiredAuthority.ShouldBe(ApprovalAuthority.Supervisor);
    }

    [Fact]
    public void BR_R_02_Handler_cannot_approve_a_supervisor_tier_transaction()
    {
        var claim = OpenClaim();
        var transaction = Submit(claim, ReserveComponentType.Indemnity, 20_000m);

        ShouldViolate(() => claim.ApproveReserveTransaction(transaction.Id, OtherHandler, Now))[ErrorKeys.ReserveApproval]
            .ShouldBe(["Your role does not have authority to approve this reserve amount."]); // VAL-13
    }

    [Fact]
    public void BR_R_02_Supervisor_can_approve_up_to_100000()
    {
        var claim = OpenClaim();
        var transaction = Submit(claim, ReserveComponentType.Indemnity, 100_000m);

        claim.ApproveReserveTransaction(transaction.Id, Supervisor, Now);

        transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.Approved);
    }

    [Fact]
    public void BR_R_02_Supervisor_cannot_approve_above_100000()
    {
        var claim = OpenClaim();
        var transaction = Submit(claim, ReserveComponentType.Indemnity, 100_000.01m);

        ShouldViolate(() => claim.ApproveReserveTransaction(transaction.Id, Supervisor, Now))[ErrorKeys.ReserveApproval]
            .ShouldBe([DomainMessages.NoApprovalAuthority]);
        transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        claim.ReserveComponents.Single().CurrentAmount.ShouldBe(0m);
    }

    [Fact]
    public void BR_R_02_Manager_can_approve_above_100000()
    {
        var claim = OpenClaim();
        var transaction = Submit(claim, ReserveComponentType.Indemnity, 250_000m);

        claim.ApproveReserveTransaction(transaction.Id, Manager, Now);

        claim.ReserveComponents.Single().CurrentAmount.ShouldBe(250_000m);
    }

    private static void AssertPending(decimal amount, ApprovalAuthority expectedTier)
    {
        var claim = OpenClaim();

        var transaction = Submit(claim, ReserveComponentType.Indemnity, amount);

        transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.PendingApproval);
        transaction.RequiredAuthority.ShouldBe(expectedTier);
        transaction.ApprovedAt.ShouldBeNull();
        transaction.PostingStatus.ShouldBe(PostingStatus.Pending);
        claim.ReserveComponents.Single().CurrentAmount.ShouldBe(0m);
        claim.ReserveComponents.Single().PendingAmount.ShouldBe(amount);

        // No approval yet, so nothing may enqueue a GL posting (BR-R-02).
        claim.DomainEvents.OfType<ReserveAutoApproved>().ShouldBeEmpty();
        claim.DomainEvents.OfType<ReserveApproved>().ShouldBeEmpty();
    }
}
