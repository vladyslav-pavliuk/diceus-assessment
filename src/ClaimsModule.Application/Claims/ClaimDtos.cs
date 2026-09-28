using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Documents;
using ClaimsModule.Domain.ReferenceData;

namespace ClaimsModule.Application.Claims;

// Read models of the claims API. Small child DTOs are positional records mapped by AutoMapper (in
// memory and with ProjectTo); the claim rows are built by hand-written SQL projections in Persistence,
// because they read shadow columns and subqueries (D-40).

/// <summary>A row of GET /api/claims (FRS §10.1 + D-29 additions).</summary>
public sealed record ClaimSummaryDto
{
    public required Guid Id { get; init; }

    public required string ClaimNumber { get; init; }

    public required Guid? PolicyId { get; init; }

    public required string? PolicyNumber { get; init; }

    public required string? ClientName { get; init; }

    public required DateTimeOffset LossDate { get; init; }

    public required string CauseOfLossCode { get; init; }

    public required string? CauseOfLossName { get; init; }

    public required ClaimStatus Status { get; init; }

    public required ClaimSeverity Severity { get; init; }

    public required Guid? AssignedHandlerId { get; init; }

    public required string? AssignedHandlerName { get; init; }

    /// <summary>Net Σ CurrentAmount over all components, Subrogation included; a display figure (D-29).</summary>
    public required decimal TotalReserves { get; init; }

    /// <summary>An SLA_BREACH_DETECTED entry exists that is newer than the claim's last update (D-01).</summary>
    public required bool IsSlaBreached { get; init; }

    public required DateTimeOffset ReportedDate { get; init; }
}

/// <summary>GET /api/claims/{id} (FRS §10.1): the claim with its children and its most recent audit entries.</summary>
public sealed record ClaimDetailDto
{
    public required Guid Id { get; init; }

    public required string ClaimNumber { get; init; }

    public required Guid? PolicyId { get; init; }

    public required string? PolicyNumber { get; init; }

    public required string? ClientName { get; init; }

    public required ClaimStatus Status { get; init; }

    public required ClaimSeverity Severity { get; init; }

    public required DateTimeOffset ReportedDate { get; init; }

    public required Guid? AssignedHandlerId { get; init; }

    public required string? AssignedHandlerName { get; init; }

    public required DateTimeOffset? ClosedAt { get; init; }

    public required string? ClosureReason { get; init; }

    public required string? Notes { get; init; }

    public required bool ReserveLimitOverride { get; init; }

    public required string? ReserveLimitOverrideReason { get; init; }

    public required DateTimeOffset? ReserveLimitOverrideAt { get; init; }

    public required bool IsSlaBreached { get; init; }

    public required decimal TotalReserves { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset? UpdatedAt { get; init; }

    public required LossEventDto LossEvent { get; init; }

    public required IReadOnlyList<ClaimPartyDto> Parties { get; init; }

    public required IReadOnlyList<RiskObjectDto> RiskObjects { get; init; }

    public required IReadOnlyList<ValidationIssueDto> ValidationIssues { get; init; }

    public required IReadOnlyList<ReserveComponentSummaryDto> ReserveComponents { get; init; }

    public required IReadOnlyList<ClaimDocumentDto> Documents { get; init; }

    public required IReadOnlyList<AuditEntryDto> RecentAuditEntries { get; init; }
}

/// <summary>
/// The loss event (FRS §9.2). <see cref="PerilCategory"/> is the brief's "ClaimType" (D-13), derived
/// from the cause of loss code.
/// </summary>
public sealed record LossEventDto
{
    public required DateTimeOffset LossDate { get; init; }

    public required string LossDescription { get; init; }

    public required string? LossLocation { get; init; }

    public required string CauseOfLossCode { get; init; }

    public required string? CauseOfLossName { get; init; }

    public required PerilCategory? PerilCategory { get; init; }

    public required decimal? EstimatedLossAmount { get; init; }

    public required DateTimeOffset ReportDate { get; init; }

    public required string? PoliceReportNumber { get; init; }
}

public sealed record ClaimPartyDto(
    Guid Id,
    PartyRole PartyRole,
    PartyType PartyType,
    string? FirstName,
    string? LastName,
    string? CompanyName,
    string DisplayName,
    string? Email,
    string? Phone,
    string? Notes,
    bool IsActive);

public sealed record RiskObjectDto(
    Guid Id,
    AssetType AssetType,
    string AssetDescription,
    string? DamageDescription,
    string? AssetReference,
    bool IsPrimary);

public sealed record ValidationIssueDto(
    Guid Id,
    string RuleCode,
    IssueSeverity Severity,
    string Field,
    string Message,
    IssueStatus Status,
    DateTimeOffset RaisedAt,
    DateTimeOffset? ResolvedAt,
    Guid? ResolvedByUserId,
    string? ResolutionNote);

/// <summary>Document metadata (FRS §9.7). The download URL comes from the documents endpoints (Phase 5).</summary>
public sealed record ClaimDocumentDto(
    Guid Id,
    DocumentType DocumentType,
    string DocumentName,
    string ContentType,
    long FileSizeBytes,
    DateTimeOffset UploadedAt,
    Guid? UploadedByUserId,
    string? Notes);

/// <summary>
/// One ClaimAuditLog row (FRS §9.8). OldValue/NewValue are the stored JSON text. A null
/// <see cref="CreatedByUserId"/> is the system actor (D-33).
/// </summary>
public sealed record AuditEntryDto
{
    public required Guid Id { get; init; }

    public required string EventType { get; init; }

    public required string Description { get; init; }

    public required string? OldValue { get; init; }

    public required string? NewValue { get; init; }

    public required Guid? RelatedEntityId { get; init; }

    public required string? RelatedEntityType { get; init; }

    public required Guid? CorrelationId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required Guid? CreatedByUserId { get; init; }

    public required string? CreatedByName { get; init; }
}
