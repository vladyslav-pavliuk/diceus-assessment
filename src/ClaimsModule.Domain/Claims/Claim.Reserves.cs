using ClaimsModule.Domain.Claims.Events;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Domain.Users;

namespace ClaimsModule.Domain.Claims;

/// <summary>The outcome of a reserve submission: the new transaction, plus non-blocking warnings (BR-R-05).</summary>
public sealed record ReserveSubmissionResult(ReserveTransaction Transaction, IReadOnlyList<string> Warnings);

// Event-sourced reserves (FRS §6, §7.2, D-05, D-11, D-21, D-22, D-23). Amounts are never updated: every
// change is a new ReserveTransaction, and a component's CurrentAmount is the projection of its history.
public sealed partial class Claim
{
    /// <summary>Σ CurrentAmount of the cost components: the figure BR-R-05 limits (D-11).</summary>
    public decimal ApprovedAggregate =>
        _reserveComponents.Where(component => ReserveLimits.CountsTowardAggregate(component.Component)).Sum(component => component.CurrentAmount);

    /// <summary>CC-04: the reserves still open at closure (components with a positive balance).</summary>
    public decimal OpenReserveTotal =>
        _reserveComponents.Where(component => component.CurrentAmount > 0).Sum(component => component.CurrentAmount);

    public bool HasPendingReserve => _reserveComponents.Any(component => component.HasPendingTransaction);

    /// <summary>FRS §4.2 → PendingPayment: "at least one reserve component with status Approved" (D-21).</summary>
    public bool HasApprovedReserve => _reserveComponents.Any(component => component.HasApprovedTransaction);

    /// <summary>
    /// POST /claims/{id}/reserves (FRS §10.2): opens or adjusts a reserve component.
    /// <list type="bullet">
    /// <item>Add opens a component: amount &gt; 0, or ≠ 0 for SubrogationRecoverable (BR-R-01).</item>
    /// <item>Adjust is a signed, non-zero delta; cost components cannot go below zero (D-05).</item>
    /// <item>Reverse brings the component to zero; the system computes the amount (D-05).</item>
    /// </list>
    /// <paramref name="transactionType"/> may be omitted: Add for a new component, Adjust otherwise.
    /// The approval tier follows |amount| (BR-R-02); ≤ $10,000 is approved at once. A transaction that
    /// would take approved reserves over $10,000,000 without the override is escalated to Manager and
    /// returns a warning (BR-R-05, D-11).
    /// </summary>
    public ReserveSubmissionResult SubmitReserveTransaction(
        ReserveComponentType componentType,
        ReserveTransactionType? transactionType,
        decimal? amount,
        string? changeReason,
        Actor submitter,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(submitter);
        EnsureModifiable();

        if (!Enum.IsDefined(componentType))
        {
            throw new BusinessRuleViolationException(ErrorKeys.ReserveComponent, DomainMessages.InvalidReserveComponent);
        }

        if (transactionType is { } requested && !Enum.IsDefined(requested))
        {
            throw new BusinessRuleViolationException(ErrorKeys.TransactionType, DomainMessages.InvalidTransactionType);
        }

        // BR-C-06: no reserves until a policy is linked.
        if (PolicyId is null)
        {
            throw new BusinessRuleViolationException(ErrorKeys.PolicyId, DomainMessages.NoPolicyLinked);
        }

        var component = _reserveComponents.SingleOrDefault(candidate => candidate.Component == componentType);

        // FRS §6.4: a pending transaction cannot be modified; retract it first (D-22: one pending per component).
        if (component is { HasPendingTransaction: true })
        {
            throw new BusinessRuleViolationException(ErrorKeys.ReserveComponent, DomainMessages.PendingTransactionExists);
        }

        var type = transactionType ?? (component is null ? ReserveTransactionType.Add : ReserveTransactionType.Adjust);
        var delta = ResolveDelta(componentType, type, component, amount);

        var reason = Text.NullIfBlank(changeReason)
            ?? throw new BusinessRuleViolationException(ErrorKeys.ChangeReason, DomainMessages.ChangeReasonRequired);

        component ??= OpenComponent(componentType);

        var exceedsLimit = WouldExceedAggregateLimit(componentType, delta);
        var requiredAuthority = exceedsLimit ? ApprovalAuthority.Manager : ReserveAuthorityPolicy.RequiredAuthorityFor(delta);

        var transaction = component.Submit(type, delta, reason, submitter.UserId, requiredAuthority, exceedsLimit, now);

        Raise(new ReserveTransactionSubmitted(
            Id,
            transaction.Id,
            component.Id,
            componentType,
            type,
            transaction.Amount,
            transaction.PreviousBalance,
            transaction.NewBalance,
            transaction.ApprovalStatus,
            transaction.RequiredAuthority,
            exceedsLimit));

        if (transaction.ApprovalStatus == ReserveApprovalStatus.AutoApproved)
        {
            Raise(new ReserveAutoApproved(Id, transaction.Id, transaction.IdempotencyKey, transaction.Amount));
        }

        return new ReserveSubmissionResult(transaction, exceedsLimit ? [DomainMessages.AggregateLimitExceeded] : []);
    }

    /// <summary>
    /// POST /claims/{id}/reserves/{txnId}/approve (FRS §6.4): not by the submitter (BR-R-03), only
    /// with enough authority (BR-R-02), and not past the $10M limit without the override (BR-R-05).
    /// </summary>
    public void ApproveReserveTransaction(Guid transactionId, Actor approver, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(approver);
        EnsureModifiable();

        var (component, transaction) = FindPendingTransaction(transactionId);

        var violations = new RuleViolations();
        if (transaction.SubmittedByUserId == approver.UserId)
        {
            violations.Add(ErrorKeys.ReserveApproval, DomainMessages.SelfApprovalNotPermitted);
        }

        if (!ReserveAuthorityPolicy.CanApprove(approver.Role, transaction.RequiredAuthority))
        {
            violations.Add(ErrorKeys.ReserveApproval, DomainMessages.NoApprovalAuthority);
        }

        // Re-checked now: other approvals may have moved the total since submission (D-11).
        if (WouldExceedAggregateLimit(component.Component, transaction.Amount))
        {
            violations.Add(ErrorKeys.ReserveAmount, DomainMessages.AggregateLimitExceeded);
        }

        violations.ThrowIfAny();

        component.Approve(transaction, approver.UserId, now);
        Raise(new ReserveApproved(Id, transaction.Id, transaction.IdempotencyKey, transaction.Amount));
    }

    /// <summary>
    /// POST /claims/{id}/reserves/{txnId}/reject (FRS §6.4 step 9). Rejecting needs the same authority
    /// as approving (FRS §3 "approve/reject reserves up to …"). The row stays in history (BR-R-04).
    /// </summary>
    public void RejectReserveTransaction(Guid transactionId, string? rejectionReason, Actor approver, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(approver);
        EnsureModifiable();

        var (_, transaction) = FindPendingTransaction(transactionId);

        var violations = new RuleViolations();
        if (!ReserveAuthorityPolicy.CanApprove(approver.Role, transaction.RequiredAuthority))
        {
            violations.Add(ErrorKeys.ReserveApproval, DomainMessages.NoRejectionAuthority);
        }

        var reason = Text.NullIfBlank(rejectionReason);
        if (reason is null)
        {
            violations.Add(ErrorKeys.RejectionReason, DomainMessages.RejectionReasonRequired);
        }

        violations.ThrowIfAny();

        transaction.Reject(approver.UserId, reason!, now);
        Raise(new ReserveRejected(Id, transaction.Id, transaction.Amount, reason!));
    }

    /// <summary>
    /// POST /claims/{id}/reserves/{txnId}/retract (FRS §6.4 rule box): only the submitter, only while
    /// pending; the row becomes Cancelled and a new transaction may then be submitted.
    /// </summary>
    public void RetractReserveTransaction(Guid transactionId, Actor submitter)
    {
        ArgumentNullException.ThrowIfNull(submitter);
        EnsureModifiable();

        var (_, transaction) = FindPendingTransaction(transactionId);
        if (transaction.SubmittedByUserId != submitter.UserId)
        {
            throw new BusinessRuleViolationException(ErrorKeys.ReserveRetraction, DomainMessages.OnlySubmitterCanRetract);
        }

        transaction.Cancel();
        Raise(new ReserveRetracted(Id, transaction.Id, transaction.Amount));
    }

    /// <summary>BR-R-05 / FRS §3: only a manager may allow approved reserves above the $10M limit.</summary>
    public void SetReserveLimitOverride(bool enabled, string? reason, Actor manager, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(manager);
        EnsureModifiable();

        if (!manager.Role.IsAtLeast(UserRole.Manager))
        {
            throw new ForbiddenAccessException("Only a manager can set the reserve limit override.");
        }

        var trimmedReason = Text.NullIfBlank(reason)
            ?? throw new BusinessRuleViolationException(ErrorKeys.ReserveLimitOverride, DomainMessages.OverrideReasonRequired);

        ReserveLimitOverride = enabled;
        ReserveLimitOverrideReason = trimmedReason;
        ReserveLimitOverrideByUserId = manager.UserId;
        ReserveLimitOverrideAt = now;
        Raise(new ReserveLimitOverrideSet(Id, enabled, trimmedReason));
    }

    /// <summary>
    /// POST /claims/{id}/reserves/{txnId}/retry-posting (FRS §11.3 "retry button for Failed", D-08): puts a
    /// failed GL posting back to Pending; the GL job is enqueued after commit. Any role may retry: the change
    /// was already approved, and posting it is idempotent. Allowed on Closed and Withdrawn claims too, because
    /// it completes the accounting of a change that was approved before the claim closed (D-41).
    /// </summary>
    public ReserveTransaction RetryGlPosting(Guid transactionId, Actor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var transaction = FindReserveTransaction(transactionId) ?? throw new NotFoundException("Reserve transaction", transactionId);
        if (!transaction.IsPostingFailed)
        {
            throw new BusinessRuleViolationException(ErrorKeys.GlPosting, DomainMessages.OnlyFailedPostingCanBeRetried);
        }

        transaction.RequeuePosting();
        Raise(new GlPostingRetryRequested(Id, transaction.Id, transaction.IdempotencyKey, transaction.Amount));
        return transaction;
    }

    public ReserveTransaction? FindReserveTransaction(Guid transactionId) =>
        _reserveComponents.Select(component => component.FindTransaction(transactionId)).FirstOrDefault(found => found is not null);

    private static decimal ResolveDelta(
        ReserveComponentType componentType, ReserveTransactionType type, ReserveComponent? component, decimal? amount)
    {
        switch (type)
        {
            case ReserveTransactionType.Add:
                if (component is not null)
                {
                    throw new BusinessRuleViolationException(ErrorKeys.TransactionType, DomainMessages.ComponentAlreadyExists(componentType));
                }

                // BR-R-01: greater than zero, except SubrogationRecoverable, which may be negative (but not zero).
                if (ReserveLimits.MayGoNegative(componentType))
                {
                    if (amount is null or 0m)
                    {
                        throw new BusinessRuleViolationException(ErrorKeys.ReserveAmount, DomainMessages.SubrogationAmountZero);
                    }
                }
                else if (amount is null or <= 0m)
                {
                    throw new BusinessRuleViolationException(ErrorKeys.ReserveAmount, DomainMessages.ReserveAmountNotPositive);
                }

                return EnsureScale(amount.Value);

            case ReserveTransactionType.Adjust:
                if (component is null)
                {
                    throw new BusinessRuleViolationException(ErrorKeys.TransactionType, DomainMessages.ComponentDoesNotExist(componentType, type));
                }

                if (amount is null or 0m)
                {
                    throw new BusinessRuleViolationException(ErrorKeys.ReserveAmount, DomainMessages.AdjustmentAmountZero);
                }

                // FRS §6.2 "May go negative?": only SubrogationRecoverable.
                if (!ReserveLimits.MayGoNegative(componentType) && component.CurrentAmount + amount.Value < 0)
                {
                    throw new BusinessRuleViolationException(ErrorKeys.ReserveAmount, DomainMessages.BalanceBelowZero(componentType));
                }

                return EnsureScale(amount.Value);

            case ReserveTransactionType.Reverse:
                if (component is null)
                {
                    throw new BusinessRuleViolationException(ErrorKeys.TransactionType, DomainMessages.ComponentDoesNotExist(componentType, type));
                }

                if (amount is not null)
                {
                    throw new BusinessRuleViolationException(ErrorKeys.ReserveAmount, DomainMessages.ReverseAmountNotAllowed);
                }

                if (component.CurrentAmount == 0)
                {
                    throw new BusinessRuleViolationException(ErrorKeys.ReserveAmount, DomainMessages.BalanceAlreadyZero(componentType));
                }

                return -component.CurrentAmount;

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown reserve transaction type.");
        }
    }

    private static decimal EnsureScale(decimal amount) =>
        Amounts.HasValidScale(amount)
            ? amount
            : throw new BusinessRuleViolationException(ErrorKeys.ReserveAmount, DomainMessages.TooManyDecimalPlaces("Reserve amount"));

    /// <summary>
    /// BR-R-05: would approving this delta take the approved aggregate over $10,000,000? Only increases
    /// of cost components can; the manager override lifts the limit (D-11). Exactly $10,000,000 is allowed.
    /// </summary>
    private bool WouldExceedAggregateLimit(ReserveComponentType componentType, decimal delta) =>
        !ReserveLimitOverride
        && ReserveLimits.CountsTowardAggregate(componentType)
        && delta > 0
        && ApprovedAggregate + delta > ReserveLimits.AggregateLimit;

    private ReserveComponent OpenComponent(ReserveComponentType componentType)
    {
        var component = ReserveComponent.Open(Id, componentType);
        _reserveComponents.Add(component);
        return component;
    }

    private (ReserveComponent Component, ReserveTransaction Transaction) FindPendingTransaction(Guid transactionId)
    {
        foreach (var component in _reserveComponents)
        {
            if (component.FindTransaction(transactionId) is { } transaction)
            {
                if (!transaction.IsPending)
                {
                    throw new BusinessRuleViolationException(ErrorKeys.ReserveApproval, DomainMessages.OnlyPendingCanBeDecided);
                }

                return (component, transaction);
            }
        }

        throw new NotFoundException("Reserve transaction", transactionId);
    }
}
