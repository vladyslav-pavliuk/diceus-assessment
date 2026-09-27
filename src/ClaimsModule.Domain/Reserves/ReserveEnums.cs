namespace ClaimsModule.Domain.Reserves;

/// <summary>FRS §6.2 (D-03).</summary>
public enum ReserveComponentType
{
    Indemnity = 1,
    Expense,
    ALAE,
    SubrogationRecoverable,
}

/// <summary>FRS §9.5. No trigger for Closed is specified, so components stay Active (D-33).</summary>
public enum ReserveComponentStatus
{
    Active = 1,
    Closed,
}

/// <summary>FRS §9.6; the rules for each type are in D-05.</summary>
public enum ReserveTransactionType
{
    Add = 1,
    Adjust,
    Reverse,
}

/// <summary>FRS §9.6. AutoApproved is equivalent to Approved everywhere (D-21).</summary>
public enum ReserveApprovalStatus
{
    AutoApproved = 1,
    PendingApproval,
    Approved,
    Rejected,
    Cancelled,
}

/// <summary>
/// FRS §9.6 GL posting state. Pending until the GL job posts an approved transaction; Cancelled
/// when a transaction is rejected or retracted and will never be posted.
/// </summary>
public enum PostingStatus
{
    Pending = 1,
    Posted,
    Failed,
    Cancelled,
}

/// <summary>Who must approve a transaction (FRS §6.3, BR-R-02).</summary>
public enum ApprovalAuthority
{
    Auto = 1,
    Supervisor,
    Manager,
}
