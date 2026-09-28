namespace ClaimsModule.Domain.Reserves;

/// <summary>BR-R-05: the aggregate limit on approved reserves per claim (D-11).</summary>
public static class ReserveLimits
{
    public const decimal AggregateLimit = 10_000_000m;

    /// <summary>An expected subrogation recovery does not free up headroom for more indemnity (D-11).</summary>
    public static bool CountsTowardAggregate(ReserveComponentType component) =>
        component != ReserveComponentType.SubrogationRecoverable;

    public static bool MayGoNegative(ReserveComponentType component) =>
        component == ReserveComponentType.SubrogationRecoverable;
}
