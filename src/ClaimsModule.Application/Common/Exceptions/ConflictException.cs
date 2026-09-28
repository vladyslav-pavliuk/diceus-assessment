namespace ClaimsModule.Application.Common.Exceptions;

/// <summary>
/// The request conflicts with one that is still being processed (for example a repeated
/// Idempotency-Key whose first request has not finished, D-24). Mapped to HTTP 409.
/// </summary>
public sealed class ConflictException(string message) : Exception(message);
