using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Reserves;

/// <summary>
/// A reserve line of one type on a claim (FRS §6.2, §9.5). An entity inside the Claim aggregate
/// (ARCHITECTURE-PLAN §2.2): the $10M limit, the no-policy rule and closure span components, so they
/// are checked on one consistent snapshot. It still has its own RowVer (FRS §15.1).
/// <para>
/// <see cref="CurrentAmount"/> is a projection of the history (Σ approved amounts, D-11), recalculated
/// in the same transaction whenever a transaction takes effect. It is never set directly.
/// </para>
/// </summary>
public sealed class ReserveComponent : Entity
{
    private readonly List<ReserveTransaction> _transactions = [];

    private ReserveComponent()
    {
    }

    private ReserveComponent(Guid id)
        : base(id)
    {
    }

    public Guid ClaimId { get; private set; }

    public ReserveComponentType Component { get; private set; }

    public decimal CurrentAmount { get; private set; }

    /// <summary>
    /// The last ChangeSequence handed out on this component (D-23). Increments at submission, so
    /// rejected and retracted transactions keep their numbers. Protected by the component's RowVer.
    /// </summary>
    public int LastChangeSequence { get; private set; }

    public ReserveComponentStatus Status { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>The full history of the component, in submission order.</summary>
    public IReadOnlyList<ReserveTransaction> Transactions => _transactions.AsReadOnly();

    public bool HasPendingTransaction => _transactions.Any(transaction => transaction.IsPending);

    public bool HasApprovedTransaction => _transactions.Any(transaction => transaction.IsApproved);

    /// <summary>The pending delta, for the reserve summary cards (FRS §11.3, D-11).</summary>
    public decimal PendingAmount => _transactions.Where(transaction => transaction.IsPending).Sum(transaction => transaction.Amount);

    internal static ReserveComponent Open(Guid claimId, ReserveComponentType component) =>
        new(SequentialGuid.NewGuid())
        {
            ClaimId = claimId,
            Component = component,
            Status = ReserveComponentStatus.Active,
        };

    internal ReserveTransaction Submit(
        ReserveTransactionType transactionType,
        decimal amount,
        string changeReason,
        Guid submittedByUserId,
        ApprovalAuthority requiredAuthority,
        bool exceedsAggregateLimit,
        DateTimeOffset now)
    {
        if (HasPendingTransaction)
        {
            throw new InvalidOperationException("A component can hold at most one pending transaction (D-22).");
        }

        LastChangeSequence++;
        var transaction = ReserveTransaction.Submit(
            this, transactionType, amount, changeReason, submittedByUserId, requiredAuthority, exceedsAggregateLimit, LastChangeSequence, now);

        _transactions.Add(transaction);
        Recalculate();
        return transaction;
    }

    internal void Approve(ReserveTransaction transaction, Guid approvedByUserId, DateTimeOffset now)
    {
        // The balance chain stays linear because nothing else can take effect on the component while
        // this transaction is pending (D-22). If that ever breaks, stop rather than store a false balance.
        if (transaction.PreviousBalance != CurrentAmount)
        {
            throw new InvalidOperationException(
                $"Reserve transaction {transaction.Id} was submitted against balance {transaction.PreviousBalance}, but the balance is now {CurrentAmount}.");
        }

        transaction.Approve(approvedByUserId, now);
        Recalculate();
    }

    internal ReserveTransaction? FindTransaction(Guid transactionId) =>
        _transactions.SingleOrDefault(transaction => transaction.Id == transactionId);

    private void Recalculate() =>
        CurrentAmount = _transactions.Where(transaction => transaction.IsApproved).Sum(transaction => transaction.Amount);
}
