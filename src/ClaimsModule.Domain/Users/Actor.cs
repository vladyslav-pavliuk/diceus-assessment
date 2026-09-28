namespace ClaimsModule.Domain.Users;

/// <summary>The user performing a domain operation, built from the validated JWT.</summary>
public sealed record Actor(Guid UserId, UserRole Role)
{
    public Guid UserId { get; } = UserId != Guid.Empty
        ? UserId
        : throw new ArgumentException("An actor must have a user id.", nameof(UserId));

    public UserRole Role { get; } = Enum.IsDefined(Role)
        ? Role
        : throw new ArgumentOutOfRangeException(nameof(Role), Role, "Unknown role.");
}
