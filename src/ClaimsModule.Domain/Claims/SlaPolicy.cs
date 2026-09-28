namespace ClaimsModule.Domain.Claims;

/// <summary>
/// FRS §12.2 SLA rule, applied by the SLA monitoring job. A claim in Draft or Open whose last update is
/// more than 48 hours old has breached the SLA; the breach is recorded at most once per 24 hours per
/// claim. The claim itself is never changed (D-01).
/// </summary>
public static class SlaPolicy
{
    /// <summary>"UpdatedAt &lt; (now − 48 hours)": strictly older than 48 hours.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(48);

    /// <summary>"only add a new entry if 24+ hours have passed": exactly 24 hours is enough.</summary>
    public static readonly TimeSpan RepeatAfter = TimeSpan.FromHours(24);

    /// <summary>The audit description, verbatim from FRS §12.2.</summary>
    public const string BreachDescription = "Claim has not been updated in 48 hours";

    public static IReadOnlyList<ClaimStatus> MonitoredStatuses { get; } = [ClaimStatus.Draft, ClaimStatus.Open];

    /// <summary>A claim last updated before this instant is stale.</summary>
    public static DateTimeOffset StaleBefore(DateTimeOffset now) => now - StaleAfter;

    /// <summary>A breach entry newer than this instant suppresses a new one.</summary>
    public static DateTimeOffset RepeatSuppressedAfter(DateTimeOffset now) => now - RepeatAfter;
}
