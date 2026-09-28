using ClaimsModule.Domain.Users;

namespace ClaimsModule.Application.Abstractions.Persistence;

/// <summary>
/// The sign-in lookups are deliberately not tenant-scoped: sign-in is what establishes the organisation, so
/// usernames are unique system-wide (D-16).
/// </summary>
public interface IUserRepository
{
    Task<User?> FindActiveByUsernameAsync(string username, CancellationToken cancellationToken);

    Task<IReadOnlyList<User>> ListActiveAsync(CancellationToken cancellationToken);

    /// <summary>Tenant-scoped, unlike the sign-in lookups.</summary>
    Task<User?> GetInOrganisationAsync(Guid userId, CancellationToken cancellationToken);
}
