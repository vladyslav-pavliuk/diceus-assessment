using ClaimsModule.Domain.Users;

namespace ClaimsModule.Application.Abstractions;

/// <summary>Read from the validated JWT. Everything is null for anonymous requests and background jobs.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? DisplayName { get; }

    UserRole? Role { get; }

    Guid? OrganisationId { get; }
}
