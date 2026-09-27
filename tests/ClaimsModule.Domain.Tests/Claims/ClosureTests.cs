using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Claims.Events;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Domain.Users;
using static ClaimsModule.Domain.Tests.TestData;

namespace ClaimsModule.Domain.Tests.Claims;

/// <summary>FRS §4.3 closure conditions CC-01..04 and BR-ST-03.</summary>
public sealed class ClosureTests
{
    /// <summary>
    /// With the FRS table a claim cannot reach Closed without having passed Draft → Open, so CC-02 and
    /// CC-03 can never fail there. The table is data (D-20: brief "Draft → Closed … if configured"), so
    /// these tests add a Draft → Closed row to prove the closure checks stand on their own.
    /// </summary>
    private static readonly StatusTransitionTable TableWithDraftClosure = new(
        ClaimStatusTransition.FrsDefaults().Append(
            ClaimStatusTransition.Create(ClaimStatus.Draft, ClaimStatus.Closed, UserRole.Handler, requiresReason: true, isSystemOnly: false)));

    [Fact]
    public void CC_01_Close_with_pending_reserve_is_rejected()
    {
        var claim = OpenClaim();
        Submit(claim, ReserveComponentType.Indemnity, 25_000m);

        var errors = ShouldViolate(() => claim.ChangeStatus(ClaimStatus.Closed, "Settled", "Justified", Handler, Transitions, Now));

        errors[ErrorKeys.StatusTransition].ShouldBe(
            ["Claim cannot be closed — CC-01 (no reserve transaction is pending approval) is not satisfied."]); // VAL-12 template
        claim.Status.ShouldBe(ClaimStatus.Open);
    }

    [Fact]
    public void CC_02_Close_with_open_critical_issue_is_rejected()
    {
        var claim = DraftClaim(withClaimant: false);

        ShouldViolate(() => claim.ChangeStatus(ClaimStatus.Closed, "Duplicate FNOL", null, Handler, TableWithDraftClosure, Now))[ErrorKeys.StatusTransition]
            .ShouldContain("Claim cannot be closed — CC-02 (no unresolved Critical validation issue) is not satisfied.");
    }

    [Fact]
    public void CC_03_Close_without_active_claimant_is_rejected()
    {
        var claim = DraftClaim(withClaimant: false);

        ShouldViolate(() => claim.ChangeStatus(ClaimStatus.Closed, "Duplicate FNOL", null, Handler, TableWithDraftClosure, Now))[ErrorKeys.StatusTransition]
            .ShouldContain("Claim cannot be closed — CC-03 (at least one active Claimant party) is not satisfied.");
    }

    [Fact]
    public void BR_ST_03_Close_lists_all_failed_conditions()
    {
        var claim = DraftClaim(withClaimant: false);
        Submit(claim, ReserveComponentType.Expense, 5_000m);    // approved: open reserve (CC-04)
        Submit(claim, ReserveComponentType.Indemnity, 75_000m); // pending (CC-01)

        var errors = ShouldViolate(() => claim.ChangeStatus(ClaimStatus.Closed, null, null, Handler, TableWithDraftClosure, Now));

        errors[ErrorKeys.StatusTransition].Length.ShouldBe(3); // CC-01, CC-02, CC-03
        errors[ErrorKeys.OpenReserves].Length.ShouldBe(2);      // CC-04 warning + requirement
        errors.ShouldContainKey(ErrorKeys.Reason);
    }

    [Fact]
    public void CC_04_Close_with_open_reserves_without_justification_is_rejected()
    {
        var claim = OpenClaim();
        Submit(claim, ReserveComponentType.Indemnity, 8_000m);

        var errors = ShouldViolate(() => claim.ChangeStatus(ClaimStatus.Closed, "Settled", "  ", Handler, Transitions, Now));

        errors[ErrorKeys.OpenReserves].ShouldBe(
        [
            "The claim has open reserves totalling 8000.00.",
            "Claim cannot be closed — CC-04 (a justification note is required to close a claim with open reserves) is not satisfied.",
        ]);
        claim.Status.ShouldBe(ClaimStatus.Open);
    }

    [Fact]
    public void CC_04_Close_with_open_reserves_and_justification_succeeds()
    {
        var claim = OpenClaim();
        Submit(claim, ReserveComponentType.Indemnity, 8_000m);

        claim.ChangeStatus(ClaimStatus.Closed, "Settled", "Final invoice expected next month.", Handler, Transitions, Now);

        claim.Status.ShouldBe(ClaimStatus.Closed);
        claim.SingleEvent<ClaimClosed>().ShouldBe(new ClaimClosed(claim.Id, "Settled", "Final invoice expected next month.", 8_000m));
    }

    [Fact]
    public void CC_04_Zero_and_negative_balances_are_not_open_reserves()
    {
        var claim = OpenClaim();
        Submit(claim, ReserveComponentType.Indemnity, 8_000m);
        Submit(claim, ReserveComponentType.Indemnity, null, type: ReserveTransactionType.Reverse);
        Submit(claim, ReserveComponentType.SubrogationRecoverable, -3_000m);

        claim.ChangeStatus(ClaimStatus.Closed, "Settled", justification: null, Handler, Transitions, Now);

        claim.Status.ShouldBe(ClaimStatus.Closed);
    }
}
