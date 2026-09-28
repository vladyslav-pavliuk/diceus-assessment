using ClaimsModule.Domain.Claims;

namespace ClaimsModule.API.Contracts.Claims;

// Request bodies of endpoints whose claim id comes from the route. The controller adds the id and
// sends the matching command; all fields are nullable so the validator, not binding, reports what is
// missing (CLAUDE.md rule 2).

/// <summary>PUT /api/claims/{id}/status (FRS §10.1, D-26).</summary>
public sealed record TransitionClaimStatusRequest(ClaimStatus? TargetStatus, string? Reason, string? Justification);

/// <summary>PUT /api/claims/{id}/policy (D-08).</summary>
public sealed record LinkPolicyRequest(Guid? PolicyId);

/// <summary>PUT /api/claims/{id}/assignee (D-08, D-18).</summary>
public sealed record AssignClaimHandlerRequest(Guid? UserId);

/// <summary>PATCH /api/claims/{id} (D-08): null leaves a field unchanged; "" clears the notes.</summary>
public sealed record UpdateClaimDetailsRequest(string? Notes, ClaimSeverity? Severity);

/// <summary>POST /api/claims/{id}/validation-issues/{issueId}/acknowledge (D-07).</summary>
public sealed record AcknowledgeValidationIssueRequest(string? Note);
