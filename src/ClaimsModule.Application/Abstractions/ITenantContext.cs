namespace ClaimsModule.Application.Abstractions;

/// <summary>
/// The organisation whose data the current unit of work may see and write (FRS §15.1, D-12, D-31).
/// HTTP requests take it from the JWT "org" claim; background jobs have no user and set it
/// explicitly. It drives the tenant query filter and stamps OrganisationId on new rows.
/// Null means "no tenant": filtered queries then return nothing, and writes are refused.
/// </summary>
public interface ITenantContext
{
    Guid? OrganisationId { get; }
}
