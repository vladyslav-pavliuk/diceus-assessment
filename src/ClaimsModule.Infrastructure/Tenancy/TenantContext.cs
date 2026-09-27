using ClaimsModule.Application.Abstractions;

namespace ClaimsModule.Infrastructure.Tenancy;

/// <summary>
/// Scoped tenant holder (D-31). By default the tenant is the caller's organisation from the JWT.
/// A background job, which has no caller, sets the organisation explicitly for its scope.
/// </summary>
public sealed class TenantContext(ICurrentUser currentUser) : ITenantContext
{
    private Guid? _organisationId;

    public Guid? OrganisationId => _organisationId ?? currentUser.OrganisationId;

    /// <summary>Sets the tenant of a scope that has no signed-in user (background jobs, tests).</summary>
    public void SetOrganisation(Guid organisationId)
    {
        if (organisationId == Guid.Empty)
        {
            throw new ArgumentException("The organisation id cannot be empty.", nameof(organisationId));
        }

        if (OrganisationId is { } current && current != organisationId)
        {
            throw new InvalidOperationException("This scope already belongs to another organisation.");
        }

        _organisationId = organisationId;
    }
}
