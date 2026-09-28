namespace ClaimsModule.Domain.Common;

/// <summary>
/// The caller's role can never perform this action (HTTP 403, D-25). Data-dependent authority
/// failures, such as a supervisor approving over $100,000, are 422 business rule violations instead.
/// </summary>
public sealed class ForbiddenAccessException(string message) : Exception(message);
