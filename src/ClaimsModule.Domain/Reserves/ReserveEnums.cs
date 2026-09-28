namespace ClaimsModule.Domain.Reserves;

public enum ReserveComponentType
{
    Indemnity = 1,
    Expense,
    ALAE,
    SubrogationRecoverable,
}

/// <summary>The FRS specifies no trigger for Closed, so components stay Active (D-33).</summary>
public enum ReserveComponentStatus
{
    Active = 1,
    Closed,
}

/// <summary>The rules for each type are in D-05.</summary>
public enum ReserveTransactionType
{
    Add = 1,
    Adjust,
    Reverse,
}

/// <summary>AutoApproved is equivalent to Approved everywhere (D-21).</summary>
public enum ReserveApprovalStatus
{
    AutoApproved = 1,
    PendingApproval,
    Approved,
    Rejected,
    Cancelled,
}

/// <summary>Cancelled means the transaction was rejected or retracted and will never be posted.</summary>
public enum PostingStatus
{
    Pending = 1,
    Posted,
    Failed,
    Cancelled,
}

public enum ApprovalAuthority
{
    Auto = 1,
    Supervisor,
    Manager,
}
