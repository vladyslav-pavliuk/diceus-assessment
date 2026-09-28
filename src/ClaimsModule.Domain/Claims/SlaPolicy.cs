namespace ClaimsModule.Domain.Claims;

/// <summary>
/// FRS §12.2: a Draft or Open claim idle for over 48 hours breaches the SLA, recorded at most once per
/// 24 hours. The claim itself is never changed (D-01).
/// </summary>
public static class SlaPolicy
{
    /// <summary>Strictly older than 48 hours.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(48);

    /// <summary>Exactly 24 hours is enough.</summary>
    public static readonly TimeSpan RepeatAfter = TimeSpan.FromHours(24);

    /// <summary>Verbatim from FRS §12.2.</summary>
    public const string BreachDescription = "Claim has not been updated in 48 hours";

    public static IReadOnlyList<ClaimStatus> MonitoredStatuses { get; } = [ClaimStatus.Draft, ClaimStatus.Open];

    public static DateTimeOffset StaleBefore(DateTimeOffset now) => now - StaleAfter;

    public static DateTimeOffset RepeatSuppressedAfter(DateTimeOffset now) => now - RepeatAfter;
}
