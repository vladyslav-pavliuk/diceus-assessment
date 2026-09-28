using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Organisations;

/// <summary>The tenant: every business row carries its OrganisationId (D-12).</summary>
public sealed class Organisation : Entity
{
    private Organisation()
    {
    }

    public string Name { get; private set; } = null!;
}
