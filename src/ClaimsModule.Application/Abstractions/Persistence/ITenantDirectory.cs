namespace ClaimsModule.Application.Abstractions.Persistence;

/// <summary>
/// The only reads that cross tenants (D-31). A background job has no user, so it must find out which
/// organisation it works for before it can set its tenant scope; after that, every query is filtered
/// by tenant as usual.
/// </summary>
public interface ITenantDirectory
{
    /// <summary>Every organisation, for the recurring jobs that run once per tenant.</summary>
    Task<IReadOnlyList<Guid>> ListOrganisationIdsAsync(CancellationToken cancellationToken);

    /// <summary>The organisation of a claim that is not soft-deleted, or null.</summary>
    Task<Guid?> FindOrganisationOfClaimAsync(Guid claimId, CancellationToken cancellationToken);
}
