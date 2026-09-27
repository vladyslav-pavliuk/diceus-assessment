namespace ClaimsModule.Application.Common.Exceptions;

/// <summary>
/// The caller's role can never perform this action, whatever the data (D-25). Mapped to HTTP 403.
/// Data-dependent authority failures (for example a supervisor approving more than $100,000) are
/// business rule violations (422), not this exception.
/// </summary>
public sealed class ForbiddenAccessException(string message) : Exception(message);
