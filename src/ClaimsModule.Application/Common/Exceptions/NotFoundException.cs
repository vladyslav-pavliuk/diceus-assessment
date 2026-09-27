namespace ClaimsModule.Application.Common.Exceptions;

/// <summary>
/// The requested resource does not exist, or is not visible to the caller's organisation (tenant
/// filter). Both cases map to HTTP 404 so that other tenants' ids are not disclosed.
/// </summary>
public sealed class NotFoundException(string resourceName, object key)
    : Exception($"{resourceName} '{key}' was not found.");
