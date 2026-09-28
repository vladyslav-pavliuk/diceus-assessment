using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Common.Paging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Documents;

namespace ClaimsModule.Application.Abstractions.ReadModels;

/// <summary>
/// The claims read side. Application owns the contract and the DTO shapes; Persistence owns the SQL,
/// because the projections read EF shadow columns (UpdatedAt, CreatedAt) and Application may not
/// reference EF Core (CONV-14, D-40). Every method runs a fixed number of queries, whatever the data
/// size (no N+1), and sees only the caller's organisation.
/// </summary>
public interface IClaimQueries
{
    Task<PagedResult<ClaimSummaryDto>> ListAsync(ClaimListFilter filter, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Null when the claim does not exist in the caller's organisation.</summary>
    Task<ClaimDetailDto?> GetDetailAsync(Guid claimId, int recentAuditEntries, CancellationToken cancellationToken);

    /// <summary>Reverse-chronological (FRS §10.1). Null when the claim does not exist in the caller's organisation.</summary>
    Task<PagedResult<AuditEntryDto>?> GetAuditAsync(Guid claimId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Every issue of the claim, oldest first. Null when the claim does not exist in the caller's organisation.</summary>
    Task<IReadOnlyList<ValidationIssueDto>?> ListValidationIssuesAsync(Guid claimId, CancellationToken cancellationToken);

    /// <summary>Reserve summary and full history (FRS §10.2). Null when the claim does not exist in the caller's organisation.</summary>
    Task<ClaimReservesDto?> GetReservesAsync(Guid claimId, CancellationToken cancellationToken);

    /// <summary>The claim's status alone. Null when the claim does not exist in the caller's organisation.</summary>
    Task<ClaimStatus?> GetStatusAsync(Guid claimId, CancellationToken cancellationToken);

    /// <summary>Every document of the claim, newest first (FRS §10.1). Null when the claim does not exist in the caller's organisation.</summary>
    Task<IReadOnlyList<ClaimDocumentRecord>?> ListDocumentsAsync(Guid claimId, CancellationToken cancellationToken);

    /// <summary>One document of the claim. Null when either does not exist in the caller's organisation.</summary>
    Task<ClaimDocumentRecord?> GetDocumentAsync(Guid claimId, Guid documentId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a document row exists, read straight from the database. Used after a failed upload commit, whose outcome
    /// may be unknown, before its blob is removed (D-42).
    /// </summary>
    Task<bool> DocumentExistsAsync(Guid documentId, CancellationToken cancellationToken);
}

/// <summary>
/// A document's stored metadata (FRS §9.7) plus the uploader's name. Carries the blob path, which the Application needs to
/// sign a download URL and which is never returned to a client.
/// </summary>
public sealed record ClaimDocumentRecord(
    Guid Id,
    DocumentType DocumentType,
    string DocumentName,
    string ContentType,
    long FileSizeBytes,
    DateTimeOffset UploadedAt,
    Guid? UploadedByUserId,
    string? UploadedByName,
    string? Notes,
    string BlobPath);

/// <summary>
/// GET /api/claims filters (FRS §10.1, D-29). Loss dates compare the UTC calendar date of the loss,
/// inclusive at both ends (D-32). <see cref="Search"/> matches part of the claim number or client name.
/// </summary>
public sealed record ClaimListFilter(
    IReadOnlyCollection<ClaimStatus> Statuses,
    DateOnly? LossDateFrom,
    DateOnly? LossDateTo,
    Guid? AssignedHandlerId,
    string? CauseOfLossCode,
    Guid? PolicyId,
    string? Search);
