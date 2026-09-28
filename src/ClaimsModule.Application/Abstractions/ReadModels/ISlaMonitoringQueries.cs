using ClaimsModule.Domain.Claims;

namespace ClaimsModule.Application.Abstractions.ReadModels;

/// <summary>The SLA job's read (FRS §12.2, D-01). Sees only the current tenant's claims.</summary>
public interface ISlaMonitoringQueries
{
    /// <summary>
    /// Claims in a monitored status (Draft, Open) last updated before <paramref name="staleBefore"/>
    /// (COALESCE(UpdatedAt, CreatedAt), D-01) that have no SLA_BREACH_DETECTED entry newer than
    /// <paramref name="lastBreachAfter"/>.
    /// </summary>
    Task<IReadOnlyList<SlaBreachCandidate>> ListBreachCandidatesAsync(
        DateTimeOffset staleBefore, DateTimeOffset lastBreachAfter, CancellationToken cancellationToken);
}

public sealed record SlaBreachCandidate(Guid ClaimId, string ClaimNumber, ClaimStatus Status, DateTimeOffset LastUpdatedAt);
