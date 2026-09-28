namespace ClaimsModule.Application.Abstractions.Persistence;

/// <summary>
/// The only cross-tenant reads (D-31): a background job uses them to find its organisation before it sets
/// its tenant scope.
/// </summary>
public interface ITenantDirectory
{
    Task<IReadOnlyList<Guid>> ListOrganisationIdsAsync(CancellationToken cancellationToken);

    Task<Guid?> FindOrganisationOfClaimAsync(Guid claimId, CancellationToken cancellationToken);
}
