namespace ClaimsModule.Domain.Common;

/// <summary>HTTP 404. Also used for rows of other tenants, so their ids are not disclosed.</summary>
public sealed class NotFoundException(string resourceName, object key)
    : Exception($"{resourceName} '{key}' was not found.");
