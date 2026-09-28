namespace ClaimsModule.Application.Common.Exceptions;

/// <summary>
/// HTTP 409: an Idempotency-Key whose first request is still running (D-24), or a concurrent change that
/// tripped a unique index.
/// </summary>
public sealed class ConflictException(string message) : Exception(message);
