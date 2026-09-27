using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Organisations;

/// <summary>
/// The tenant. Every business row carries an OrganisationId (FRS §15.1, D-12).
/// A single organisation is seeded; its id lives in the seed data and in configuration, never in logic.
/// </summary>
public sealed class Organisation : Entity
{
    private Organisation()
    {
    }

    public string Name { get; private set; } = null!;
}
