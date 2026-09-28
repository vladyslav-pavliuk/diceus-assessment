namespace ClaimsModule.Application.Claims.Queries;

/// <summary>Paging defaults and caps for the claim reads (D-29, D-33; ASSUMPTION where noted in D-40).</summary>
public static class ClaimQueryLimits
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public const int DefaultAuditPageSize = 50;
    public const int MaxAuditPageSize = 200;

    /// <summary>How many of the latest audit entries GET /api/claims/{id} includes.</summary>
    public const int RecentAuditEntries = 10;

    public const int MaxSearchLength = 100;
}
