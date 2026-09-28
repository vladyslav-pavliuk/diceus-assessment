using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Reserves;

/// <summary>
/// One reserve transaction: a row of the append-only ReserveHistory table (FRS §6.6, §9.6).
/// <para>
/// Amount-immutable, not row-immutable (D-22): the amount, balances, sequence, key and submitter are
/// fixed at submission and guarded by a SaveChanges interceptor; only the approval and posting
/// columns move, once, through the methods below. A change of amount is always a new row.
/// </para>
/// <para>
/// GL posting (FRS §6.5, §12.1): Pending → Posted, or Pending → Failed once the job's retries are
/// exhausted. Those two moves are made by the GL job as compare-and-set UPDATEs in the database
/// (ARCHITECTURE-PLAN §6.1 R6/R8), not through this class, because the check and the write must be one
/// atomic statement. The only move made here is the user's retry, Failed → Pending.
/// </para>
/// </summary>
public sealed class ReserveTransaction : Entity
{
    private ReserveTransaction()
    {
    }

    private ReserveTransaction(Guid id)
        : base(id)
    {
    }

    public Guid ReserveComponentId { get; private set; }

    /// <summary>Denormalised from the component for query convenience (FRS §9.6).</summary>
    public Guid ClaimId { get; private set; }

    public ReserveTransactionType TransactionType { get; private set; }

    /// <summary>The signed delta: positive increases the reserve, negative decreases it (FRS §9.6).</summary>
    public decimal Amount { get; private set; }

    public decimal PreviousBalance { get; private set; }

    public decimal NewBalance { get; private set; }

    public ReserveApprovalStatus ApprovalStatus { get; private set; }

    public ApprovalAuthority RequiredAuthority { get; private set; }

    /// <summary>True when approving this transaction would take the claim over the $10M limit (BR-R-05).</summary>
    public bool ExceedsAggregateLimit { get; private set; }

    public Guid? ApprovedByUserId { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public Guid? RejectedByUserId { get; private set; }

    public DateTimeOffset? RejectedAt { get; private set; }

    public string? RejectionReason { get; private set; }

    public string ChangeReason { get; private set; } = null!;

    public PostingStatus PostingStatus { get; private set; }

    public string? PostingJobId { get; private set; }

    public string IdempotencyKey { get; private set; } = null!;

    public int ChangeSequence { get; private set; }

    public Guid SubmittedByUserId { get; private set; }

    public bool IsPending => ApprovalStatus == ReserveApprovalStatus.PendingApproval;

    /// <summary>Approved and AutoApproved both count toward the balance (D-11, D-21).</summary>
    public bool IsApproved => ApprovalStatus is ReserveApprovalStatus.Approved or ReserveApprovalStatus.AutoApproved;

    /// <summary>The GL job gave up on this approved transaction; a user may retry it (D-08).</summary>
    public bool IsPostingFailed => IsApproved && PostingStatus == PostingStatus.Failed;

    internal static ReserveTransaction Submit(
        ReserveComponent component,
        ReserveTransactionType transactionType,
        decimal amount,
        string changeReason,
        Guid submittedByUserId,
        ApprovalAuthority requiredAuthority,
        bool exceedsAggregateLimit,
        int changeSequence,
        DateTimeOffset now)
    {
        var autoApproved = requiredAuthority == ApprovalAuthority.Auto;

        return new ReserveTransaction(SequentialGuid.NewGuid())
        {
            ReserveComponentId = component.Id,
            ClaimId = component.ClaimId,
            TransactionType = transactionType,
            Amount = amount,

            // Valid for pending rows too: a component has at most one pending transaction, so nothing
            // else can change its balance before this one is decided (D-22).
            PreviousBalance = component.CurrentAmount,
            NewBalance = component.CurrentAmount + amount,
            ApprovalStatus = autoApproved ? ReserveApprovalStatus.AutoApproved : ReserveApprovalStatus.PendingApproval,
            RequiredAuthority = requiredAuthority,
            ExceedsAggregateLimit = exceedsAggregateLimit,

            // Auto-approval has no approving user (ASSUMPTION, D-39); the status says who decided.
            ApprovedAt = autoApproved ? now : null,
            ChangeReason = changeReason,
            PostingStatus = PostingStatus.Pending,
            IdempotencyKey = GlIdempotencyKey.For(component.Id, changeSequence),
            ChangeSequence = changeSequence,
            SubmittedByUserId = submittedByUserId,
        };
    }

    internal void Approve(Guid approvedByUserId, DateTimeOffset now)
    {
        EnsurePending();
        ApprovalStatus = ReserveApprovalStatus.Approved;
        ApprovedByUserId = approvedByUserId;
        ApprovedAt = now;
    }

    internal void Reject(Guid rejectedByUserId, string reason, DateTimeOffset now)
    {
        EnsurePending();
        ApprovalStatus = ReserveApprovalStatus.Rejected;
        RejectedByUserId = rejectedByUserId;
        RejectedAt = now;
        RejectionReason = reason;
        PostingStatus = PostingStatus.Cancelled;
    }

    /// <summary>A failed GL posting goes back to Pending, so the GL job can post it again (D-08 retry-posting).</summary>
    internal void RequeuePosting()
    {
        if (!IsPostingFailed)
        {
            throw new InvalidOperationException($"Reserve transaction {Id} has GL posting status {PostingStatus}, not a failed posting of an approved transaction.");
        }

        PostingStatus = PostingStatus.Pending;
    }

    internal void Cancel()
    {
        EnsurePending();
        ApprovalStatus = ReserveApprovalStatus.Cancelled;
        PostingStatus = PostingStatus.Cancelled;
    }

    private void EnsurePending()
    {
        if (!IsPending)
        {
            throw new InvalidOperationException($"Reserve transaction {Id} is {ApprovalStatus}, not pending approval.");
        }
    }
}
