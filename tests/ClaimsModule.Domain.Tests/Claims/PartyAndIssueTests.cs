using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Claims.Events;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Users;
using static ClaimsModule.Domain.Tests.TestData;

namespace ClaimsModule.Domain.Tests.Claims;

public sealed class PartyAndIssueTests
{
    [Fact]
    public void PTY_01_Removing_last_active_claimant_is_rejected()
    {
        var claim = OpenClaim();
        var claimant = claim.Parties.Single(party => party.IsActiveClaimant);

        ShouldViolate(() => claim.RemoveParty(claimant.Id, Handler, Now))[ErrorKeys.ClaimParties]
            .ShouldBe(["The last active Claimant cannot be removed."]);
        claimant.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void PTY_01_A_claimant_can_be_removed_while_another_is_active()
    {
        var claim = OpenClaim();
        var second = claim.AddParty(Claimant("Anna"), Handler, Now);

        claim.RemoveParty(second.Id, Handler, Now);

        claim.Parties.Count(party => party.IsActiveClaimant).ShouldBe(1);
    }

    [Fact]
    public void PTY_01_Removed_party_is_inactive_not_deleted()
    {
        var claim = OpenClaim();
        var witness = claim.AddParty(Witness(), Handler, Now);

        claim.RemoveParty(witness.Id, Handler, Now);

        claim.Parties.ShouldContain(witness);
        witness.IsActive.ShouldBeFalse();
        claim.SingleEvent<PartyRemoved>().ShouldBe(new PartyRemoved(claim.Id, witness.Id, PartyRole.Witness, "John Smith"));
    }

    [Fact]
    public void PTY_01_Removing_an_already_removed_party_is_rejected()
    {
        var claim = OpenClaim();
        var witness = claim.AddParty(Witness(), Handler, Now);
        claim.RemoveParty(witness.Id, Handler, Now);

        ShouldViolate(() => claim.RemoveParty(witness.Id, Handler, Now))[ErrorKeys.ClaimParties]
            .ShouldBe(["The party has already been removed from the claim."]);
    }

    [Fact]
    public void PTY_01_Removing_an_unknown_party_is_not_found()
    {
        Should.Throw<NotFoundException>(() => OpenClaim().RemoveParty(Guid.NewGuid(), Handler, Now));
    }

    [Fact]
    public void D_07_Acknowledging_a_warning_requires_a_note()
    {
        var claim = DraftClaim(withPolicy: false);
        var warning = claim.ValidationIssues.Single();

        ShouldViolate(() => claim.AcknowledgeValidationIssue(warning.Id, " ", Handler, Now))[ErrorKeys.Note]
            .ShouldBe(["An acknowledgement note is required."]);
    }

    [Fact]
    public void D_07_Critical_issues_cannot_be_acknowledged()
    {
        var claim = DraftClaim(withClaimant: false);
        var critical = claim.ValidationIssues.Single();

        ShouldViolate(() => claim.AcknowledgeValidationIssue(critical.Id, "Waive it", Manager, Now))[ErrorKeys.ValidationIssue]
            .ShouldBe(["Only a Warning can be acknowledged."]);
    }

    [Fact]
    public void D_07_An_issue_is_acknowledged_once()
    {
        var claim = DraftClaim(withPolicy: false);
        var warning = claim.ValidationIssues.Single();
        claim.AcknowledgeValidationIssue(warning.Id, "Policy search pending", Handler, Now);

        ShouldViolate(() => claim.AcknowledgeValidationIssue(warning.Id, "Again", Handler, Now))[ErrorKeys.ValidationIssue]
            .ShouldBe(["Only an open validation issue can be acknowledged."]);
        claim.SingleEvent<ValidationIssueAcknowledged>().Note.ShouldBe("Policy search pending");
    }

    [Fact]
    public void D_07_Revalidation_keeps_one_active_issue_per_rule()
    {
        var claim = DraftClaim(withPolicy: false, withClaimant: false, withRiskObject: false);

        claim.Revalidate(null, Now);
        claim.Revalidate(null, Now);

        claim.ValidationIssues.Count.ShouldBe(3);
        claim.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void D_07_Revalidation_requires_the_linked_policy()
    {
        var claim = DraftClaim();

        Should.Throw<ArgumentException>(() => claim.Revalidate(InForcePolicy(), Now));
    }

    [Fact]
    public void D_07_Changing_the_policy_resets_the_period_acknowledgement()
    {
        var claim = DraftClaim(policy: PolicyCovering(new DateOnly(2020, 1, 1), new DateOnly(2021, 1, 1)));
        var first = claim.ValidationIssues.Single();
        claim.AcknowledgeValidationIssue(first.Id, "Accepted", Handler, Now);

        claim.LinkPolicy(PolicyCovering(new DateOnly(2022, 1, 1), new DateOnly(2023, 1, 1)), Handler, Now);

        first.Status.ShouldBe(IssueStatus.Resolved);
        claim.ValidationIssues.Single(issue => issue.Status == IssueStatus.Open).RuleCode.ShouldBe(ValidationRuleCodes.LossDateOutsidePolicyPeriod);
    }

    [Fact]
    public void D_18_Only_a_supervisor_or_manager_can_assign_the_handler()
    {
        var claim = DraftClaim();
        var assignee = TestUsers.Active(UserRole.Handler);

        Should.Throw<ForbiddenAccessException>(() => claim.AssignHandler(assignee, Handler));

        claim.AssignHandler(assignee, Supervisor);

        claim.AssignedHandlerId.ShouldBe(assignee.Id);
        claim.SingleEvent<HandlerAssigned>().PreviousHandlerId.ShouldBe(Handler.UserId);
    }

    [Fact]
    public void D_08_Notes_and_severity_changes_raise_events()
    {
        var claim = DraftClaim();

        claim.UpdateNotes("  Called the claimant.  ", Handler);
        claim.ChangeSeverity(ClaimSeverity.Critical, Handler);
        claim.UpdateNotes("Called the claimant.", Handler); // unchanged: no event

        claim.Notes.ShouldBe("Called the claimant.");
        claim.DomainEvents.OfType<ClaimDetailsUpdated>().Select(updated => updated.Field).ShouldBe(["Notes", "Severity"]);
    }
}
