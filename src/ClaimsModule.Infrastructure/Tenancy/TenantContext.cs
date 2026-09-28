using ClaimsModule.Application.Abstractions;

namespace ClaimsModule.Infrastructure.Tenancy;

/// <summary>The caller's organisation by default; background jobs set it explicitly (D-31).</summary>
public sealed class TenantContext(ICurrentUser currentUser) : ITenantContext
{
    private Guid? _organisationId;

    public Guid? OrganisationId => _organisationId ?? currentUser.OrganisationId;

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
