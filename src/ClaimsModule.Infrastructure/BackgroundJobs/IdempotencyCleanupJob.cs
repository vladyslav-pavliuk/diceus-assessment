using ClaimsModule.Application.Abstractions;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// Deletes Idempotency-Key records older than 24 hours (D-24), daily. Housekeeping of an HTTP-layer table, not a
/// business action, so it calls the store directly rather than through a command, and writes no audit row.
/// </summary>
public sealed class IdempotencyCleanupJob(IIdempotencyStore store, TimeProvider timeProvider, ILogger<IdempotencyCleanupJob> logger)
{
    public const string RecurringJobId = "idempotency-cleanup";
    public const string Schedule = "0 3 * * *";

    /// <summary>How long a key can be replayed (D-24, ASSUMPTION).</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    [AutomaticRetry(Attempts = 0)]
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var deleted = await store.PurgeAsync(timeProvider.GetUtcNow() - Retention, cancellationToken);
        logger.LogInformation("Deleted {Count} idempotency records older than {Retention}", deleted, Retention);
        return deleted;
    }
}
