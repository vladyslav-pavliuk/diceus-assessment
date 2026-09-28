using ClaimsModule.Domain.Claims.Events;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Domain.Users;

namespace ClaimsModule.Domain.Claims;

/// <summary>Warnings are non-blocking (BR-R-05).</summary>
public sealed record ReserveSubmissionResult(ReserveTransaction Transaction, IReadOnlyList<string> Warnings);

// Event-sourced reserves: every change is a new ReserveTransaction, and a component's CurrentAmount is the
// projection of its history. Amounts are never updated.
public sealed partial class Claim
{
    /// <summary>The figure BR-R-05 limits: cost components only (D-11).</summary>
    public decimal ApprovedAggregate =>
        _reserveComponents.Where(component => ReserveLimits.CountsTowardAggregate(component.Component)).Sum(component => component.CurrentAmount);

    /// <summary>CC-04: components with a positive balance.</summary>
    public decimal OpenReserveTotal =>
        _reserveComponents.Where(component => component.CurrentAmount > 0).Sum(component => component.CurrentAmount);

    public bool HasPendingReserve => _reserveComponents.Any(component => component.HasPendingTransaction);

    /// <summary>Entry condition of PendingPayment (D-21).</summary>
    public bool HasApprovedReserve => _reserveComponents.Any(component => component.HasApprovedTransaction);

    /// <summary>
    /// Add opens a component, Adjust applies a signed delta, Reverse brings it to zero (D-05). An omitted type means
    /// Add for a new component and Adjust otherwise. A transaction that would breach the $10,000,000 limit is
    /// escalated to Manager with a warning instead of being refused (BR-R-05, D-11).
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

        // One pending transaction per component; retract it first (D-22).
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

    /// <summary>BR-R-02 authority, BR-R-03 no self-approval, BR-R-05 limit re-checked at approval time.</summary>
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
    /// Needs the same authority as approving and, like approving, someone other than the submitter, who
    /// retracts instead (D-45). The row stays in history (BR-R-04).
    /// </summary>
    public void RejectReserveTransaction(Guid transactionId, string? rejectionReason, Actor approver, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(approver);
        EnsureModifiable();

        var (_, transaction) = FindPendingTransaction(transactionId);

        var violations = new RuleViolations();
        if (transaction.SubmittedByUserId == approver.UserId)
        {
            violations.Add(ErrorKeys.ReserveApproval, DomainMessages.SelfRejectionNotPermitted);
        }

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

    /// <summary>Only the submitter, only while pending; the row becomes Cancelled (FRS §6.4).</summary>
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
    /// Any role may retry, since the change is already approved and posting is idempotent. Allowed on Closed and
    /// Withdrawn claims too: it completes the accounting of a change approved before closure (D-41).
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

                // BR-R-01: SubrogationRecoverable may be negative, but never zero.
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

    /// <summary>BR-R-05: exactly $10,000,000 is allowed, and only increases of cost components count (D-11).</summary>
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
