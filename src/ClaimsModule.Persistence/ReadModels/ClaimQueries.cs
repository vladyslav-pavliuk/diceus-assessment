using AutoMapper;
using AutoMapper.QueryableExtensions;
using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Claims;
using ClaimsModule.Application.Common.Paging;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.ReadModels;

/// <summary>
/// Every method runs a fixed number of queries (no N+1). Claim rows are hand-written projections, because they read shadow
/// columns and correlated subqueries; child rows use ProjectTo (D-40).
/// </summary>
internal sealed class ClaimQueries(ClaimsDbContext dbContext, IMapper mapper) : IClaimQueries
{
    public async Task<PagedResult<ClaimSummaryDto>> ListAsync(ClaimListFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var claims = Filter(dbContext.Claims.AsNoTracking(), filter);

        var totalCount = await claims.CountAsync(cancellationToken);
        var items = await ToSummaries(claims
                .OrderByDescending(claim => claim.ReportedDate)
                .ThenByDescending(claim => claim.ClaimNumber)
                .Skip(page.Skip)
                .Take(page.PageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<ClaimSummaryDto>(items, totalCount, page.Page, page.PageSize);
    }

    public async Task<ClaimDetailDto?> GetDetailAsync(Guid claimId, int recentAuditEntries, CancellationToken cancellationToken)
    {
        var header = await dbContext.Claims.AsNoTracking()
            .Where(claim => claim.Id == claimId)
            .Select(claim => new
            {
                claim.Id,
                claim.ClaimNumber,
                claim.PolicyId,
                claim.PolicyNumber,
                claim.ClientName,
                claim.Status,
                claim.Severity,
                claim.ReportedDate,
                claim.AssignedHandlerId,
                AssignedHandlerName = dbContext.Users.Where(user => user.Id == claim.AssignedHandlerId).Select(user => user.DisplayName).FirstOrDefault(),
                claim.ClosedAt,
                claim.ClosureReason,
                claim.Notes,
                claim.ReserveLimitOverride,
                claim.ReserveLimitOverrideReason,
                claim.ReserveLimitOverrideAt,
                IsSlaBreached = dbContext.ClaimAuditLog.Any(entry =>
                    entry.ClaimId == claim.Id
                    && entry.EventType == AuditEventTypes.SlaBreachDetected
                    && entry.CreatedAt > (EF.Property<DateTimeOffset?>(claim, ShadowColumns.UpdatedAt) ?? EF.Property<DateTimeOffset>(claim, ShadowColumns.CreatedAt))),
                TotalReserves = claim.ReserveComponents.Sum(component => component.CurrentAmount),
                CreatedAt = EF.Property<DateTimeOffset>(claim, ShadowColumns.CreatedAt),
                UpdatedAt = EF.Property<DateTimeOffset?>(claim, ShadowColumns.UpdatedAt),
                LossEvent = new LossEventDto
                {
                    LossDate = claim.LossEvent.LossDate,
                    LossDescription = claim.LossEvent.LossDescription,
                    LossLocation = claim.LossEvent.LossLocation,
                    CauseOfLossCode = claim.LossEvent.CauseOfLossCode,
                    CauseOfLossName = dbContext.CauseOfLossCodes
                        .Where(code => code.Code == claim.LossEvent.CauseOfLossCode).Select(code => code.Name).FirstOrDefault(),
                    PerilCategory = dbContext.CauseOfLossCodes
                        .Where(code => code.Code == claim.LossEvent.CauseOfLossCode).Select(code => (Domain.ReferenceData.PerilCategory?)code.PerilCategory).FirstOrDefault(),
                    EstimatedLossAmount = claim.LossEvent.EstimatedLossAmount,
                    ReportDate = claim.LossEvent.ReportDate,
                    PoliceReportNumber = claim.LossEvent.PoliceReportNumber,
                },
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return null;
        }

        var parties = await dbContext.Set<ClaimParty>().AsNoTracking()
            .Where(party => party.ClaimId == claimId)
            .OrderBy(party => EF.Property<DateTimeOffset>(party, ShadowColumns.CreatedAt)).ThenBy(party => party.Id)
            .ProjectTo<ClaimPartyDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        var riskObjects = await dbContext.Set<ClaimRiskObject>().AsNoTracking()
            .Where(riskObject => riskObject.ClaimId == claimId)
            .OrderBy(riskObject => EF.Property<DateTimeOffset>(riskObject, ShadowColumns.CreatedAt)).ThenBy(riskObject => riskObject.Id)
            .ProjectTo<RiskObjectDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        var validationIssues = await ValidationIssues(claimId).ToListAsync(cancellationToken);

        var reserveComponents = await ReserveSummaries(claimId).ToListAsync(cancellationToken);

        var documents = await dbContext.Set<ClaimDocument>().AsNoTracking()
            .Where(document => document.ClaimId == claimId)
            .OrderByDescending(document => document.UploadedAt)
            .ProjectTo<ClaimDocumentDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        var recentAudit = await ToAuditEntries(NewestFirst(claimId).Take(recentAuditEntries)).ToListAsync(cancellationToken);

        return new ClaimDetailDto
        {
            Id = header.Id,
            ClaimNumber = header.ClaimNumber,
            PolicyId = header.PolicyId,
            PolicyNumber = header.PolicyNumber,
            ClientName = header.ClientName,
            Status = header.Status,
            Severity = header.Severity,
            ReportedDate = header.ReportedDate,
            AssignedHandlerId = header.AssignedHandlerId,
            AssignedHandlerName = header.AssignedHandlerName,
            ClosedAt = header.ClosedAt,
            ClosureReason = header.ClosureReason,
            Notes = header.Notes,
            ReserveLimitOverride = header.ReserveLimitOverride,
            ReserveLimitOverrideReason = header.ReserveLimitOverrideReason,
            ReserveLimitOverrideAt = header.ReserveLimitOverrideAt,
            IsSlaBreached = header.IsSlaBreached,
            TotalReserves = header.TotalReserves,
            CreatedAt = header.CreatedAt,
            UpdatedAt = header.UpdatedAt,
            LossEvent = header.LossEvent,
            Parties = parties,
            RiskObjects = riskObjects,
            ValidationIssues = validationIssues,

            // Stored as text, so SQL would sort alphabetically rather than in FRS §6.2 order.
            ReserveComponents = reserveComponents.OrderBy(component => component.Component).ToList(),
            Documents = documents,
            RecentAuditEntries = recentAudit,
        };
    }

    public async Task<PagedResult<AuditEntryDto>?> GetAuditAsync(Guid claimId, PageRequest page, CancellationToken cancellationToken)
    {
        if (!await ClaimExistsAsync(claimId, cancellationToken))
        {
            return null;
        }

        var totalCount = await dbContext.ClaimAuditLog.CountAsync(entry => entry.ClaimId == claimId, cancellationToken);
        var items = await ToAuditEntries(NewestFirst(claimId).Skip(page.Skip).Take(page.PageSize)).ToListAsync(cancellationToken);

        return new PagedResult<AuditEntryDto>(items, totalCount, page.Page, page.PageSize);
    }

    public async Task<IReadOnlyList<ValidationIssueDto>?> ListValidationIssuesAsync(Guid claimId, CancellationToken cancellationToken)
    {
        if (!await ClaimExistsAsync(claimId, cancellationToken))
        {
            return null;
        }

        return await ValidationIssues(claimId).ToListAsync(cancellationToken);
    }

    /// <summary>Three queries, whatever the size of the history.</summary>
    public async Task<ClaimReservesDto?> GetReservesAsync(Guid claimId, CancellationToken cancellationToken)
    {
        var claim = await dbContext.Claims.AsNoTracking()
            .Where(candidate => candidate.Id == claimId)
            .Select(candidate => new { candidate.Id, candidate.ReserveLimitOverride })
            .SingleOrDefaultAsync(cancellationToken);

        if (claim is null)
        {
            return null;
        }

        var components = await ReserveSummaries(claimId).ToListAsync(cancellationToken);

        var transactions = await dbContext.ReserveHistory.AsNoTracking()
            .Where(transaction => transaction.ClaimId == claimId)
            .OrderByDescending(transaction => EF.Property<DateTimeOffset>(transaction, ShadowColumns.CreatedAt))
            .ThenByDescending(transaction => transaction.Id)
            .Select(transaction => new ReserveHistoryEntryDto
            {
                Id = transaction.Id,
                ReserveComponentId = transaction.ReserveComponentId,
                Component = dbContext.Set<ReserveComponent>()
                    .Where(component => component.Id == transaction.ReserveComponentId).Select(component => component.Component).First(),
                TransactionType = transaction.TransactionType,
                Amount = transaction.Amount,
                PreviousBalance = transaction.PreviousBalance,
                NewBalance = transaction.NewBalance,
                ApprovalStatus = transaction.ApprovalStatus,
                RequiredAuthority = transaction.RequiredAuthority,
                ExceedsAggregateLimit = transaction.ExceedsAggregateLimit,
                ChangeReason = transaction.ChangeReason,
                ChangeSequence = transaction.ChangeSequence,
                IdempotencyKey = transaction.IdempotencyKey,
                PostingStatus = transaction.PostingStatus,
                PostingJobId = transaction.PostingJobId,
                CreatedAt = EF.Property<DateTimeOffset>(transaction, ShadowColumns.CreatedAt),
                SubmittedByUserId = transaction.SubmittedByUserId,
                SubmittedByName = dbContext.Users.Where(user => user.Id == transaction.SubmittedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                ApprovedByUserId = transaction.ApprovedByUserId,
                ApprovedByName = dbContext.Users.Where(user => user.Id == transaction.ApprovedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                ApprovedAt = transaction.ApprovedAt,
                RejectedByUserId = transaction.RejectedByUserId,
                RejectedByName = dbContext.Users.Where(user => user.Id == transaction.RejectedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
                RejectedAt = transaction.RejectedAt,
                RejectionReason = transaction.RejectionReason,
            })
            .ToListAsync(cancellationToken);

        return new ClaimReservesDto
        {
            ClaimId = claim.Id,

            // Stored as text, so SQL would sort alphabetically rather than in FRS §6.2 order.
            Components = components.OrderBy(component => component.Component).ToList(),
            Transactions = transactions,
            TotalReserves = components.Sum(component => component.CurrentAmount),
            ApprovedAggregate = components
                .Where(component => ReserveLimits.CountsTowardAggregate(component.Component))
                .Sum(component => component.CurrentAmount),
            AggregateLimit = ReserveLimits.AggregateLimit,
            ReserveLimitOverride = claim.ReserveLimitOverride,
        };
    }

    private static IQueryable<Claim> Filter(IQueryable<Claim> claims, ClaimListFilter filter)
    {
        if (filter.Statuses.Count > 0)
        {
            var statuses = filter.Statuses.ToList();
            claims = claims.Where(claim => statuses.Contains(claim.Status));
        }

        // The loss date's UTC calendar date, inclusive at both ends (D-32).
        if (filter.LossDateFrom is { } from)
        {
            var fromInstant = StartOfUtcDay(from);
            claims = claims.Where(claim => claim.LossEvent.LossDate >= fromInstant);
        }

        if (filter.LossDateTo is { } to)
        {
            var toExclusive = StartOfUtcDay(to.AddDays(1));
            claims = claims.Where(claim => claim.LossEvent.LossDate < toExclusive);
        }

        if (filter.AssignedHandlerId is { } handlerId)
        {
            claims = claims.Where(claim => claim.AssignedHandlerId == handlerId);
        }

        if (filter.CauseOfLossCode is { } causeOfLossCode)
        {
            claims = claims.Where(claim => claim.LossEvent.CauseOfLossCode == causeOfLossCode);
        }

        if (filter.PolicyId is { } policyId)
        {
            claims = claims.Where(claim => claim.PolicyId == policyId);
        }

        // EF parameterises the term and escapes LIKE wildcards.
        if (filter.Search is { } search)
        {
            claims = claims.Where(claim => claim.ClaimNumber.Contains(search) || (claim.ClientName != null && claim.ClientName.Contains(search)));
        }

        return claims;
    }

    private IQueryable<ClaimSummaryDto> ToSummaries(IQueryable<Claim> claims) =>
        claims.Select(claim => new ClaimSummaryDto
        {
            Id = claim.Id,
            ClaimNumber = claim.ClaimNumber,
            PolicyId = claim.PolicyId,
            PolicyNumber = claim.PolicyNumber,
            ClientName = claim.ClientName,
            LossDate = claim.LossEvent.LossDate,
            CauseOfLossCode = claim.LossEvent.CauseOfLossCode,
            CauseOfLossName = dbContext.CauseOfLossCodes
                .Where(code => code.Code == claim.LossEvent.CauseOfLossCode).Select(code => code.Name).FirstOrDefault(),
            Status = claim.Status,
            Severity = claim.Severity,
            AssignedHandlerId = claim.AssignedHandlerId,
            AssignedHandlerName = dbContext.Users.Where(user => user.Id == claim.AssignedHandlerId).Select(user => user.DisplayName).FirstOrDefault(),

            // Net of all components, subrogation included (D-29).
            TotalReserves = claim.ReserveComponents.Sum(component => component.CurrentAmount),

            // The SLA job never writes the claim, so any later change clears the flag (D-01).
            IsSlaBreached = dbContext.ClaimAuditLog.Any(entry =>
                entry.ClaimId == claim.Id
                && entry.EventType == AuditEventTypes.SlaBreachDetected
                && entry.CreatedAt > (EF.Property<DateTimeOffset?>(claim, ShadowColumns.UpdatedAt) ?? EF.Property<DateTimeOffset>(claim, ShadowColumns.CreatedAt))),
            ReportedDate = claim.ReportedDate,
        });

    /// <summary>Rows of one transaction share a timestamp, so the sequential id (D-30) breaks ties in write order.</summary>
    private IQueryable<ClaimAuditLog> NewestFirst(Guid claimId) =>
        dbContext.ClaimAuditLog.AsNoTracking()
            .Where(entry => entry.ClaimId == claimId)
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id);

    private IQueryable<AuditEntryDto> ToAuditEntries(IQueryable<ClaimAuditLog> entries) =>
        entries.Select(entry => new AuditEntryDto
        {
            Id = entry.Id,
            EventType = entry.EventType,
            Description = entry.Description,
            OldValue = entry.OldValue,
            NewValue = entry.NewValue,
            RelatedEntityId = entry.RelatedEntityId,
            RelatedEntityType = entry.RelatedEntityType,
            CorrelationId = entry.CorrelationId,
            CreatedAt = entry.CreatedAt,
            CreatedByUserId = entry.CreatedByUserId,
            CreatedByName = dbContext.Users.Where(user => user.Id == entry.CreatedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
        });

    private IQueryable<ValidationIssueDto> ValidationIssues(Guid claimId) =>
        dbContext.Set<ClaimValidationIssue>().AsNoTracking()
            .Where(issue => issue.ClaimId == claimId)
            .OrderBy(issue => issue.RaisedAt).ThenBy(issue => issue.Id)
            .ProjectTo<ValidationIssueDto>(mapper.ConfigurationProvider);

    private IQueryable<ReserveComponentSummaryDto> ReserveSummaries(Guid claimId) =>
        dbContext.Set<ReserveComponent>().AsNoTracking()
            .Where(component => component.ClaimId == claimId)
            .Select(component => new ReserveComponentSummaryDto
            {
                Id = component.Id,
                Component = component.Component,
                CurrentAmount = component.CurrentAmount,
                PendingAmount = component.Transactions
                    .Where(transaction => transaction.ApprovalStatus == ReserveApprovalStatus.PendingApproval)
                    .Sum(transaction => transaction.Amount),
                HasPendingApproval = component.Transactions.Any(transaction => transaction.ApprovalStatus == ReserveApprovalStatus.PendingApproval),
                Status = component.Status,
            });

    public async Task<ClaimStatus?> GetStatusAsync(Guid claimId, CancellationToken cancellationToken) =>
        await dbContext.Claims.AsNoTracking()
            .Where(claim => claim.Id == claimId)
            .Select(claim => (ClaimStatus?)claim.Status)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ClaimDocumentRecord>?> ListDocumentsAsync(Guid claimId, CancellationToken cancellationToken)
    {
        if (!await ClaimExistsAsync(claimId, cancellationToken))
        {
            return null;
        }

        // Ordered before the projection: EF cannot see through the record's constructor.
        return await ToDocumentRecords(Documents()
                .Where(document => document.ClaimId == claimId)
                .OrderByDescending(document => document.UploadedAt).ThenByDescending(document => document.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClaimDocumentRecord?> GetDocumentAsync(Guid claimId, Guid documentId, CancellationToken cancellationToken) =>
        await ToDocumentRecords(Documents().Where(document => document.ClaimId == claimId && document.Id == documentId))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> DocumentExistsAsync(Guid documentId, CancellationToken cancellationToken) =>
        Documents().AnyAsync(document => document.Id == documentId, cancellationToken);

    private IQueryable<ClaimDocument> Documents() => dbContext.Set<ClaimDocument>().AsNoTracking();

    private IQueryable<ClaimDocumentRecord> ToDocumentRecords(IQueryable<ClaimDocument> documents) =>
        documents.Select(document => new ClaimDocumentRecord(
            document.Id,
            document.DocumentType,
            document.DocumentName,
            document.ContentType,
            document.FileSizeBytes,
            document.UploadedAt,
            document.UploadedByUserId,
            dbContext.Users.Where(user => user.Id == document.UploadedByUserId).Select(user => user.DisplayName).FirstOrDefault(),
            document.Notes,
            document.BlobPath));

    private Task<bool> ClaimExistsAsync(Guid claimId, CancellationToken cancellationToken) =>
        dbContext.Claims.AnyAsync(claim => claim.Id == claimId, cancellationToken);

    private static DateTimeOffset StartOfUtcDay(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
