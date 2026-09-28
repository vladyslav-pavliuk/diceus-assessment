using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Users;

/// <summary>A seeded user of the mock authentication (D-16); user management is out of scope.</summary>
public sealed class User : Entity
{
    private User()
    {
    }

    private User(Guid id)
        : base(id)
    {
    }

    public Guid OrganisationId { get; private set; }

    public string Username { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>For tests: users are seeded (D-16).</summary>
    public static User Create(Guid organisationId, string username, string displayName, UserRole role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role.");
        }

        return new User(SequentialGuid.NewGuid())
        {
            OrganisationId = organisationId,
            Username = username,
            DisplayName = displayName,
            Role = role,
            IsActive = true,
        };
    }
}
