namespace ClaimsModule.Application.Common.Exceptions;

/// <summary>
/// The request conflicts with another one: a repeated Idempotency-Key whose first request has not finished
/// (D-24), or a concurrent change that committed first and tripped a unique index (ARCHITECTURE-PLAN §6.1 M2).
/// Mapped to HTTP 409.
/// </summary>
public sealed class ConflictException(string message) : Exception(message);
