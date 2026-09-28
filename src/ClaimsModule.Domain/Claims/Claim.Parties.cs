using ClaimsModule.Domain.Claims.Events;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Policies;
using ClaimsModule.Domain.Users;

namespace ClaimsModule.Domain.Claims;

// Parties, risk objects and the validation issues they drive (D-07).
public sealed partial class Claim
{
    public bool HasActiveClaimant => _parties.Any(party => party.IsActiveClaimant);

    /// <summary>Several parties may share a role (BR-P-02).</summary>
    public ClaimParty AddParty(PartyDetails details, Actor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(actor);
        EnsureModifiable();

        var party = AddPartyCore(details);
        EvaluateCompletenessIssues(now);
        return party;
    }

    /// <summary>Deactivates the party (D-27). The last active Claimant cannot be removed, in any status.</summary>
    public void RemoveParty(Guid partyId, Actor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        EnsureModifiable();

        var party = _parties.SingleOrDefault(candidate => candidate.Id == partyId)
            ?? throw new NotFoundException("Party", partyId);

        if (!party.IsActive)
        {
            throw new BusinessRuleViolationException(ErrorKeys.ClaimParties, DomainMessages.PartyAlreadyRemoved);
        }

        if (party.IsActiveClaimant && _parties.Count(candidate => candidate.IsActiveClaimant) == 1)
        {
            throw new BusinessRuleViolationException(ErrorKeys.ClaimParties, DomainMessages.LastClaimantCannotBeRemoved);
        }

        party.Deactivate();
        Raise(new PartyRemoved(Id, party.Id, party.PartyRole, party.DisplayName));
        EvaluateCompletenessIssues(now);
    }

    /// <summary>The first risk object is primary unless another is marked primary (D-33).</summary>
    public ClaimRiskObject AddRiskObject(RiskObjectDetails details, Actor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(actor);
        EnsureModifiable();

        var riskObject = AddRiskObjectCore(details);
        EvaluateCompletenessIssues(now);
        return riskObject;
    }

    /// <summary>
    /// Only Warnings can be acknowledged; Criticals cannot be waived (D-07). Acknowledging BR-C-02 unblocks
    /// Draft → Open (D-19).
    /// </summary>
    public void AcknowledgeValidationIssue(Guid issueId, string? note, Actor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        EnsureModifiable();

        var issue = _validationIssues.SingleOrDefault(candidate => candidate.Id == issueId)
            ?? throw new NotFoundException("Validation issue", issueId);

        var violations = new RuleViolations();
        if (issue.Severity != IssueSeverity.Warning)
        {
            violations.Add(ErrorKeys.ValidationIssue, DomainMessages.OnlyWarningsCanBeAcknowledged);
        }
        else if (issue.Status != IssueStatus.Open)
        {
            violations.Add(ErrorKeys.ValidationIssue, DomainMessages.OnlyOpenIssuesCanBeAcknowledged);
        }

        var trimmedNote = Text.NullIfBlank(note);
        if (trimmedNote is null)
        {
            violations.Add(ErrorKeys.Note, DomainMessages.AcknowledgementNoteRequired);
        }

        violations.ThrowIfAny();

        issue.Acknowledge(actor.UserId, trimmedNote!, now);
        Raise(new ValidationIssueAcknowledged(Id, issue.Id, issue.RuleCode, trimmedNote!));
    }

    private ClaimParty AddPartyCore(PartyDetails details)
    {
        var party = ClaimParty.Create(Id, details);
        _parties.Add(party);
        Raise(new PartyAdded(Id, party.Id, party.PartyRole, party.DisplayName));
        return party;
    }

    private ClaimRiskObject AddRiskObjectCore(RiskObjectDetails details)
    {
        var isPrimary = details.IsPrimary || _riskObjects.Count == 0;
        var riskObject = ClaimRiskObject.Create(Id, details, isPrimary);

        if (isPrimary)
        {
            foreach (var other in _riskObjects)
            {
                other.ClearPrimary();
            }
        }

        _riskObjects.Add(riskObject);
        Raise(new RiskObjectAdded(Id, riskObject.Id, riskObject.AssetType, riskObject.AssetDescription));
        return riskObject;
    }

    /// <summary>The issues that depend only on the claim itself (D-06).</summary>
    private void EvaluateCompletenessIssues(DateTimeOffset now)
    {
        Evaluate(
            failing: !HasActiveClaimant,
            ValidationRuleCodes.NoClaimant, IssueSeverity.Critical, ErrorKeys.ClaimParties, DomainMessages.ClaimantRequired,
            resolutionNote: "An active Claimant was added.",
            now);

        Evaluate(
            failing: PolicyId is null,
            ValidationRuleCodes.NoPolicy, IssueSeverity.Warning, ErrorKeys.PolicyId, DomainMessages.NoPolicyLinked,
            resolutionNote: "A policy was linked.",
            now);

        Evaluate(
            failing: _riskObjects.Count == 0,
            ValidationRuleCodes.NoRiskObject, IssueSeverity.Warning, ErrorKeys.RiskObjects, DomainMessages.NoRiskObjects,
            resolutionNote: "A risk object was added.",
            now);
    }

    /// <summary>BR-C-02. Without a policy there is nothing to check.</summary>
    private void EvaluatePolicyPeriodIssue(Policy? policy, DateTimeOffset now) =>
        Evaluate(
            failing: policy is not null && !policy.CoversLossDate(LossEvent.LossDate),
            ValidationRuleCodes.LossDateOutsidePolicyPeriod, IssueSeverity.Warning, ErrorKeys.LossDate, DomainMessages.LossDateOutsidePolicyPeriod,
            resolutionNote: "The loss date is within the linked policy's period.",
            now);

    private void Evaluate(
        bool failing, string ruleCode, IssueSeverity severity, string field, string message, string resolutionNote, DateTimeOffset now)
    {
        if (!failing)
        {
            ResolveIssues(ruleCode, resolutionNote, now);
            return;
        }

        // One active issue per rule: an Open or Acknowledged issue already says it.
        if (_validationIssues.Any(issue => issue.RuleCode == ruleCode && issue.IsActive))
        {
            return;
        }

        var raised = ClaimValidationIssue.Raise(Id, ruleCode, severity, field, message, now);
        _validationIssues.Add(raised);
        Raise(new ValidationIssueRaised(Id, raised.Id, ruleCode, severity, field, message));
    }

    private void ResolveIssues(string ruleCode, string note, DateTimeOffset now)
    {
        foreach (var issue in _validationIssues.Where(issue => issue.RuleCode == ruleCode && issue.IsActive))
        {
            issue.Resolve(note, now);
            Raise(new ValidationIssueResolved(Id, issue.Id, ruleCode, note));
        }
    }
}
