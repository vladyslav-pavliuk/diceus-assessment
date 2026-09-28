namespace ClaimsModule.Domain.Claims;

/// <summary>
/// Keys of the "errors" object in a 422 body (FRS §10.4). Where FRS §8 names the field, its name is
/// used; the others name the input or condition that failed.
/// </summary>
public static class ErrorKeys
{
    // FRS §8 "Field" column.
    public const string LossDate = "LossDate";
    public const string LossDescription = "LossDescription";
    public const string CauseOfLossCode = "CauseOfLossCode";
    public const string PolicyId = "PolicyId";
    public const string ClaimParties = "ClaimParties";
    public const string ReserveAmount = "ReserveAmount";
    public const string ReserveComponent = "ReserveComponent";
    public const string StatusTransition = "StatusTransition";
    public const string ReserveApproval = "ReserveApproval";

    // Additional keys (ASSUMPTION: the FRS does not name them).
    public const string Claim = "Claim";
    public const string ClaimNumber = "ClaimNumber";
    public const string Severity = "Severity";
    public const string EstimatedLossAmount = "EstimatedLossAmount";
    public const string RiskObjects = "RiskObjects";
    public const string Reason = "Reason";
    public const string OpenReserves = "OpenReserves";
    public const string AssignedHandlerId = "AssignedHandlerId";
    public const string Reserves = "Reserves";
    public const string TransactionType = "TransactionType";
    public const string ChangeReason = "ChangeReason";
    public const string RejectionReason = "RejectionReason";
    public const string ReserveRetraction = "ReserveRetraction";
    public const string ReserveLimitOverride = "ReserveLimitOverride";
    public const string GlPosting = "GlPosting";
    public const string ValidationIssue = "ValidationIssue";
    public const string Note = "Note";
    public const string Document = "Document";

    // Document upload (D-42): keyed by the multipart form fields, so the UI can put each message next to its control.
    public const string File = "File";
    public const string DocumentType = "DocumentType";
    public const string Notes = "Notes";
}
