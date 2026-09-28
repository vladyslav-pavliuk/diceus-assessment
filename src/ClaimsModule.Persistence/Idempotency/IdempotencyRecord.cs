namespace ClaimsModule.Persistence.Idempotency;

/// <summary>
/// One request made with an Idempotency-Key (D-24). A web-layer bookkeeping row, not a business
/// record, so it lives in Persistence (like the claim-number counter) and has no soft-delete or audit
/// columns (D-14). A row with no <see cref="CompletedAt"/> is a request still in progress.
/// </summary>
internal sealed class IdempotencyRecord
{
    public const int KeyMaxLength = 200;

    public Guid Id { get; private set; }

    public Guid OrganisationId { get; private set; }

    public Guid UserId { get; private set; }

    public string Key { get; private set; } = null!;

    public string Method { get; private set; } = null!;

    public string Route { get; private set; } = null!;

    /// <summary>SHA-256 (hex) of method, path, query and body: "the same request".</summary>
    public string RequestHash { get; private set; } = null!;

    public int? StatusCode { get; private set; }

    public string? ResponseBody { get; private set; }

    public string? ResponseLocation { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }
}
