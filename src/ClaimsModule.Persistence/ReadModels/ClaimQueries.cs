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
/// The claims read side (FRS §10.1). Every method projects in SQL and runs a fixed number of queries,
/// whatever the number of claims, parties or entries (no N+1). Claim rows use hand-written Select
/// projections, because they read shadow columns (UpdatedAt, CreatedAt) and correlated subqueries
/// (names, reserve totals, SLA flag); the simple child rows use AutoMapper's ProjectTo with the
/// Application profiles (D-40). The tenant and soft-delete filters apply to every table read here.
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

        // One query per collection: a constant number, independent of how many rows each one has.
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

        var reserveComponents = await dbContext.Set<ReserveComponent>().AsNoTracking()
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
            })
            .ToListAsync(cancellationToken);

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

            // Stored as text, so SQL would sort them alphabetically; the enum order is the FRS §6.2 order.
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

    private static IQueryable<Claim> Filter(IQueryable<Claim> claims, ClaimListFilter filter)
    {
        if (filter.Statuses.Count > 0)
        {
            var statuses = filter.Statuses.ToList();
            claims = claims.Where(claim => statuses.Contains(claim.Status));
        }

        // D-32: the loss date's UTC calendar date, inclusive at both ends.
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

        // FRS §10.1: "partial claim number or client name". EF parameterises the term and escapes LIKE wildcards.
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

            // D-29: net of all components, SubrogationRecoverable included.
            TotalReserves = claim.ReserveComponents.Sum(component => component.CurrentAmount),

            // D-01: a breach entry newer than the claim's last update. The SLA job never writes the claim,
            // so any later change to the claim clears the flag.
            IsSlaBreached = dbContext.ClaimAuditLog.Any(entry =>
                entry.ClaimId == claim.Id
                && entry.EventType == AuditEventTypes.SlaBreachDetected
                && entry.CreatedAt > (EF.Property<DateTimeOffset?>(claim, ShadowColumns.UpdatedAt) ?? EF.Property<DateTimeOffset>(claim, ShadowColumns.CreatedAt))),
            ReportedDate = claim.ReportedDate,
        });

    /// <summary>
    /// Reverse-chronological (FRS §10.1). Rows of one transaction can share a timestamp, so the id breaks
    /// ties: ids are sequential in SQL Server order (D-30), i.e. in the order the rows were written.
    /// </summary>
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

    private Task<bool> ClaimExistsAsync(Guid claimId, CancellationToken cancellationToken) =>
        dbContext.Claims.AnyAsync(claim => claim.Id == claimId, cancellationToken);

    private static DateTimeOffset StartOfUtcDay(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
