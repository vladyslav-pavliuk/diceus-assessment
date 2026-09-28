namespace ClaimsModule.Application.Abstractions;

/// <summary>
/// Storage for the Idempotency-Key header (FRS §10, D-24). The API filter that uses it records one
/// request per (user, key) and replays the stored response when the same request arrives again.
/// Rows are written outside the command's transaction: the placeholder must be visible to a
/// concurrent duplicate before the command runs.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>Claims the key for this request, or reports why it cannot (replay, still running, or a different request).</summary>
    Task<IdempotencyClaim> BeginAsync(IdempotentRequest request, CancellationToken cancellationToken);

    /// <summary>Stores the response of a successful request so a repeat can replay it.</summary>
    Task CompleteAsync(Guid recordId, StoredResponse response, CancellationToken cancellationToken);

    /// <summary>Frees the key after a failed request, so the client may retry with it.</summary>
    Task ReleaseAsync(Guid recordId, CancellationToken cancellationToken);

    /// <summary>Deletes the records created before <paramref name="createdBefore"/> (24h retention, D-24); returns how many.</summary>
    Task<int> PurgeAsync(DateTimeOffset createdBefore, CancellationToken cancellationToken);
}

/// <summary>What identifies a request: who sent it, under which key, to where, with which body (SHA-256 hex).</summary>
public sealed record IdempotentRequest(Guid UserId, string Key, string Method, string Route, string RequestHash);

public sealed record StoredResponse(int StatusCode, string Body, string? Location);

public abstract record IdempotencyClaim
{
    private IdempotencyClaim()
    {
    }

    /// <summary>The key is new: run the request, then complete or release <see cref="RecordId"/>.</summary>
    public sealed record Started(Guid RecordId) : IdempotencyClaim;

    /// <summary>The same request already succeeded: send <see cref="Response"/> again.</summary>
    public sealed record Replay(StoredResponse Response) : IdempotencyClaim;

    /// <summary>The same request is still running.</summary>
    public sealed record InProgress : IdempotencyClaim;

    /// <summary>The key was used for a different method, route or body.</summary>
    public sealed record Mismatch : IdempotencyClaim;
}
