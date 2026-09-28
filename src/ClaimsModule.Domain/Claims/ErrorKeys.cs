namespace ClaimsModule.Domain.Claims;

/// <summary>Keys of the "errors" object in a 422 body (FRS §10.4).</summary>
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

    // Not named by the FRS.
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

    // Multipart form fields of the document upload (D-42), so the UI shows each message next to its control.
    public const string File = "File";
    public const string DocumentType = "DocumentType";
    public const string Notes = "Notes";
}
