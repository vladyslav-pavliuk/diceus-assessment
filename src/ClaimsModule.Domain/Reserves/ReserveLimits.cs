namespace ClaimsModule.Domain.Reserves;

/// <summary>BR-R-05: the aggregate limit on approved reserves per claim (D-11).</summary>
public static class ReserveLimits
{
    public const decimal AggregateLimit = 10_000_000m;

    /// <summary>
    /// SubrogationRecoverable is an expected recovery, so it does not create headroom for more
    /// indemnity: the aggregate counts only the cost components (D-11, ASSUMPTION).
    /// </summary>
    public static bool CountsTowardAggregate(ReserveComponentType component) =>
        component != ReserveComponentType.SubrogationRecoverable;

    /// <summary>Only SubrogationRecoverable may go negative (FRS §6.2).</summary>
    public static bool MayGoNegative(ReserveComponentType component) =>
        component == ReserveComponentType.SubrogationRecoverable;
}
