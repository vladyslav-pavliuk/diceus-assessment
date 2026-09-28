using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Common.Paging;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Documents;

namespace ClaimsModule.Application.Abstractions.ReadModels;

/// <summary>
/// Implemented in Persistence, because the projections read EF shadow columns (D-40). Each method runs a fixed
/// number of queries and returns null when the claim is not in the caller's organisation.
/// </summary>
public interface IClaimQueries
{
    Task<PagedResult<ClaimSummaryDto>> ListAsync(ClaimListFilter filter, PageRequest page, CancellationToken cancellationToken);

    Task<ClaimDetailDto?> GetDetailAsync(Guid claimId, int recentAuditEntries, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<PagedResult<AuditEntryDto>?> GetAuditAsync(Guid claimId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Oldest first.</summary>
    Task<IReadOnlyList<ValidationIssueDto>?> ListValidationIssuesAsync(Guid claimId, CancellationToken cancellationToken);

    Task<ClaimReservesDto?> GetReservesAsync(Guid claimId, CancellationToken cancellationToken);

    Task<ClaimStatus?> GetStatusAsync(Guid claimId, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<ClaimDocumentRecord>?> ListDocumentsAsync(Guid claimId, CancellationToken cancellationToken);

    Task<ClaimDocumentRecord?> GetDocumentAsync(Guid claimId, Guid documentId, CancellationToken cancellationToken);

    /// <summary>Reads the database directly, to learn the outcome of an upload commit that failed ambiguously (D-42).</summary>
    Task<bool> DocumentExistsAsync(Guid documentId, CancellationToken cancellationToken);
}

/// <summary>Carries the blob path for signing download URLs; it is never returned to a client.</summary>
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
/// Loss dates compare the UTC calendar date, inclusive at both ends (D-32). <see cref="Search"/> matches part of
/// the claim number or client name.
/// </summary>
public sealed record ClaimListFilter(
    IReadOnlyCollection<ClaimStatus> Statuses,
    DateOnly? LossDateFrom,
    DateOnly? LossDateTo,
    Guid? AssignedHandlerId,
    string? CauseOfLossCode,
    Guid? PolicyId,
    string? Search);
