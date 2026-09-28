namespace ClaimsModule.Application.Abstractions;

/// <summary>
/// Backs the Idempotency-Key header (D-24). Writes happen outside the command's transaction, so a concurrent
/// duplicate sees the placeholder before the command runs.
/// </summary>
public interface IIdempotencyStore
{
    Task<IdempotencyClaim> BeginAsync(IdempotentRequest request, CancellationToken cancellationToken);

    Task CompleteAsync(Guid recordId, StoredResponse response, CancellationToken cancellationToken);

    /// <summary>Frees the key after a failed request, so the client may retry with it.</summary>
    Task ReleaseAsync(Guid recordId, CancellationToken cancellationToken);

    /// <summary>Returns how many records were deleted.</summary>
    Task<int> PurgeAsync(DateTimeOffset createdBefore, CancellationToken cancellationToken);
}

/// <summary><see cref="RequestHash"/> is the SHA-256 hex of the body.</summary>
public sealed record IdempotentRequest(Guid UserId, string Key, string Method, string Route, string RequestHash);

public sealed record StoredResponse(int StatusCode, string Body, string? Location);

public abstract record IdempotencyClaim
{
    private IdempotencyClaim()
    {
    }

    /// <summary>Run the request, then complete or release <see cref="RecordId"/>.</summary>
    public sealed record Started(Guid RecordId) : IdempotencyClaim;

    public sealed record Replay(StoredResponse Response) : IdempotencyClaim;

    public sealed record InProgress : IdempotencyClaim;

    /// <summary>The key was used for a different method, route or body.</summary>
    public sealed record Mismatch : IdempotencyClaim;
}
