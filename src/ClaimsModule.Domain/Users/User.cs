using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Users;

/// <summary>
/// A claims professional who can sign in through the mock authentication (D-16).
/// Users are seeded via HasData; there is no user-management feature in scope.
/// </summary>
public sealed class User : Entity
{
    private User()
    {
    }

    public Guid OrganisationId { get; private set; }

    public string Username { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }
}
