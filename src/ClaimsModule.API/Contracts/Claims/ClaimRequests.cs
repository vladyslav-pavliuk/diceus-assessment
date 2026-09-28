using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Documents;
using ClaimsModule.Domain.Reserves;

namespace ClaimsModule.API.Contracts.Claims;

// Every field is nullable, so the validator rather than model binding reports what is missing.

public sealed record TransitionClaimStatusRequest(ClaimStatus? TargetStatus, string? Reason, string? Justification);

public sealed record LinkPolicyRequest(Guid? PolicyId);

public sealed record AssignClaimHandlerRequest(Guid? UserId);

/// <summary>Null leaves a field unchanged; "" clears the notes.</summary>
public sealed record UpdateClaimDetailsRequest(string? Notes, ClaimSeverity? Severity);

public sealed record AcknowledgeValidationIssueRequest(string? Note);

public sealed record SetReserveLimitOverrideRequest(bool? Enabled, string? Reason);

/// <summary><c>transactionType</c> may be omitted (D-05).</summary>
public sealed record SubmitReserveRequest(
    ReserveComponentType? Component, decimal? Amount, string? ChangeReason, ReserveTransactionType? TransactionType);

/// <summary>The component's new absolute balance (D-04).</summary>
public sealed record AdjustReserveRequest(decimal? NewAmount, string? ChangeReason);

public sealed record RejectReserveRequest(string? RejectionReason);

/// <summary><see cref="DocumentType"/> defaults to Other (D-42).</summary>
public sealed class UploadDocumentForm
{
    public IFormFile? File { get; init; }

    public DocumentType? DocumentType { get; init; }

    public string? Notes { get; init; }
}
