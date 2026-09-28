using ClaimsModule.Domain.Claims;

namespace ClaimsModule.Application.Abstractions.ReadModels;

public interface ISlaMonitoringQueries
{
    /// <summary>
    /// Last update is COALESCE(UpdatedAt, CreatedAt) (D-01); claims with an SLA_BREACH_DETECTED entry newer
    /// than <paramref name="lastBreachAfter"/> are skipped.
    /// </summary>
    Task<IReadOnlyList<SlaBreachCandidate>> ListBreachCandidatesAsync(
        DateTimeOffset staleBefore, DateTimeOffset lastBreachAfter, CancellationToken cancellationToken);
}

public sealed record SlaBreachCandidate(Guid ClaimId, string ClaimNumber, ClaimStatus Status, DateTimeOffset LastUpdatedAt);
