namespace ClaimsModule.Domain.Common;

/// <summary>
/// The requested resource does not exist, or is not visible to the caller's organisation (tenant
/// filter). Both cases map to HTTP 404 so that other tenants' ids are not disclosed.
/// Lives in the Domain so that an aggregate can report an unknown child id (a party, a reserve
/// transaction) the same way a handler reports an unknown claim.
/// </summary>
public sealed class NotFoundException(string resourceName, object key)
    : Exception($"{resourceName} '{key}' was not found.");
