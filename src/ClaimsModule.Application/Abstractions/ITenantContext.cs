namespace ClaimsModule.Application.Abstractions;

/// <summary>
/// Drives the tenant query filter and stamps OrganisationId on new rows (D-12, D-31). Null means no tenant:
/// filtered queries return nothing and writes are refused.
/// </summary>
public interface ITenantContext
{
    Guid? OrganisationId { get; }
}
