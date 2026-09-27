using ClaimsModule.Domain.Claims.Events;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Users;

namespace ClaimsModule.Domain.Claims;

// The status state machine (FRS §4, §7.3, D-09, D-26).
public sealed partial class Claim
{
    /// <summary>
    /// PUT /claims/{id}/status (FRS §10.1). The transition must be a row of
    /// <paramref name="transitions"/> (BR-ST-01), the actor must hold its minimum role (D-09), and the
    /// target's entry conditions must hold (BR-ST-02, BR-ST-03 / CC-01..04, §4.2). Every failed
    /// condition is reported at once. Reopen moves on to Open in the same call (BR-ST-04).
    /// </summary>
    /// <param name="reason">Closure, withdrawal or reopen reason; required where the row says so.</param>
    /// <param name="justification">CC-04: confirms closing a claim whose reserves are still open.</param>
    public void ChangeStatus(
        ClaimStatus target,
        string? reason,
        string? justification,
        Actor actor,
        StatusTransitionTable transitions,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(transitions);

        var transition = transitions.Find(Status, target);
        if (transition is null || transition.IsSystemOnly)
        {
            throw new BusinessRuleViolationException(new Dictionary<string, string[]>
            {
                [ErrorKeys.StatusTransition] =
                [
                    DomainMessages.TransitionNotPermitted(Status, target),
                    DomainMessages.ValidNextStatuses(transitions.ValidNextStatuses(Status)),
                ],
            });
        }

        // A role that can never make this move gets 403, not 422 (D-25).
        if (!actor.Role.IsAtLeast(transition.MinimumRole!.Value))
        {
            throw new ForbiddenAccessException(
                $"The {transition.MinimumRole.Value.ToCode()} role is required to move a claim from {Status} to {target}.");
        }

        var trimmedReason = Text.NullIfBlank(reason);
        var trimmedJustification = Text.NullIfBlank(justification);

        var violations = new RuleViolations();
        if (transition.RequiresReason && trimmedReason is null)
        {
            violations.Add(ErrorKeys.Reason, DomainMessages.ReasonRequired(target));
        }

        switch (target)
        {
            case ClaimStatus.Open:
                CheckOpenConditions(violations);
                break;
            case ClaimStatus.PendingPayment when !HasApprovedReserve:
                violations.Add(ErrorKeys.Reserves, DomainMessages.ApprovedReserveRequired);
                break;
            case ClaimStatus.Closed:
                CheckClosureConditions(violations, trimmedJustification);
                break;
            case ClaimStatus.Withdrawn when HasPendingReserve:
                violations.Add(ErrorKeys.Reserves, DomainMessages.PendingReserveBlocksWithdrawal);
                break;
        }

        violations.ThrowIfAny();

        switch (target)
        {
            case ClaimStatus.Closed:
                ClosedAt = now;
                ClosureReason = trimmedReason;
                MoveTo(ClaimStatus.Closed, trimmedReason);
                Raise(new ClaimClosed(Id, trimmedReason!, trimmedJustification, OpenReserveTotal));
                break;

            case ClaimStatus.Withdrawn:
                ClosedAt = now;
                ClosureReason = trimmedReason;
                MoveTo(ClaimStatus.Withdrawn, trimmedReason);
                break;

            case ClaimStatus.Reopened:
                Reopen(trimmedReason!, transitions);
                break;

            default:
                MoveTo(target, trimmedReason);
                break;
        }
    }

    /// <summary>BR-ST-02 (+ D-18, D-19): the conditions to enter Open by request.</summary>
    private void CheckOpenConditions(RuleViolations violations)
    {
        if (!HasActiveClaimant)
        {
            violations.Add(ErrorKeys.ClaimParties, DomainMessages.ClaimantRequired);
        }

        // BR-C-03 is reported by the claimant check above; any other open Critical issue blocks too.
        foreach (var issue in _validationIssues.Where(issue => issue.IsOpenCritical && issue.RuleCode != ValidationRuleCodes.NoClaimant))
        {
            violations.Add(issue.Field, issue.Message);
        }

        // D-19: the out-of-period warning must be cleared or acknowledged first (BR-C-02).
        if (_validationIssues.Any(issue => issue.RuleCode == ValidationRuleCodes.LossDateOutsidePolicyPeriod && issue.Status == IssueStatus.Open))
        {
            violations.Add(ErrorKeys.LossDate, DomainMessages.LossDateOutsidePolicyPeriodNotAcknowledged);
        }

        if (AssignedHandlerId is null)
        {
            violations.Add(ErrorKeys.AssignedHandlerId, DomainMessages.HandlerRequiredToOpen);
        }
    }

    /// <summary>FRS §4.3 CC-01..04 / BR-ST-03: every failed condition is listed.</summary>
    private void CheckClosureConditions(RuleViolations violations, string? justification)
    {
        if (HasPendingReserve)
        {
            violations.Add(ErrorKeys.StatusTransition, DomainMessages.ClosureConditionNotSatisfied(DomainMessages.ConditionNoPendingReserves));
        }

        if (_validationIssues.Any(issue => issue.IsOpenCritical))
        {
            violations.Add(ErrorKeys.StatusTransition, DomainMessages.ClosureConditionNotSatisfied(DomainMessages.ConditionNoOpenCriticalIssues));
        }

        if (!HasActiveClaimant)
        {
            violations.Add(ErrorKeys.StatusTransition, DomainMessages.ClosureConditionNotSatisfied(DomainMessages.ConditionActiveClaimant));
        }

        // CC-04: open reserves are a warning that the handler confirms with a justification note.
        // A distinct key lets the UI's pre-flight checklist ask for the note (D-26).
        if (OpenReserveTotal > 0 && justification is null)
        {
            violations.Add(ErrorKeys.OpenReserves, DomainMessages.OpenReservesWarning(OpenReserveTotal));
            violations.Add(ErrorKeys.OpenReserves, DomainMessages.ClosureConditionNotSatisfied(DomainMessages.ConditionOpenReservesJustified));
        }
    }

    /// <summary>
    /// BR-ST-04: Closed → Reopened, then immediately Reopened → Open through the system-only row, in
    /// the same unit of work. Audit: STATUS_CHANGED, CLAIM_REOPENED, STATUS_CHANGED (D-26).
    /// </summary>
    private void Reopen(string reason, StatusTransitionTable transitions)
    {
        var automatic = transitions.Find(ClaimStatus.Reopened, ClaimStatus.Open);
        if (automatic is not { IsSystemOnly: true })
        {
            throw new InvalidOperationException("The transition table has no system-only Reopened → Open row (FRS §4.2, D-09).");
        }

        MoveTo(ClaimStatus.Reopened, reason);
        Raise(new ClaimReopened(Id, reason));

        // The earlier closure stays in the audit log (D-26).
        ClosedAt = null;
        ClosureReason = null;
        MoveTo(ClaimStatus.Open, reason: null);
    }

    private void MoveTo(ClaimStatus target, string? reason)
    {
        var from = Status;
        Status = target;
        Raise(new ClaimStatusChanged(Id, from, target, reason));
    }
}
