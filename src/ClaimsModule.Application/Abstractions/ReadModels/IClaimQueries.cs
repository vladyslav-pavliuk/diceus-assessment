using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Common.Paging;
using ClaimsModule.Domain.Claims;

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
}

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
