using ClaimsModule.Domain.Users;

namespace ClaimsModule.Application.Abstractions;

/// <summary>
/// The caller of the current request, read from the validated JWT (claims sub, name, role, org).
/// All properties are null when the request is anonymous. Background jobs have no user; they
/// act as the system actor (D-33) and set their own tenant scope (D-31).
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? DisplayName { get; }

    UserRole? Role { get; }

    Guid? OrganisationId { get; }
}
