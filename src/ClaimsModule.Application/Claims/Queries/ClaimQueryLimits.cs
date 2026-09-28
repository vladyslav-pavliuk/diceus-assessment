namespace ClaimsModule.Application.Claims.Queries;

/// <summary>D-29, D-33, D-40.</summary>
public static class ClaimQueryLimits
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    public const int DefaultAuditPageSize = 50;
    public const int MaxAuditPageSize = 200;

    /// <summary>Included in the claim detail.</summary>
    public const int RecentAuditEntries = 10;

    public const int MaxSearchLength = 100;
}
