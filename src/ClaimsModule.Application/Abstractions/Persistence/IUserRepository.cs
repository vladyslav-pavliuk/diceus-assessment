using ClaimsModule.Domain.Users;

namespace ClaimsModule.Application.Abstractions.Persistence;

/// <summary>
/// Read access to seeded users. The two sign-in lookups (D-16) are deliberately not tenant-scoped:
/// at sign-in the caller has no organisation yet, and the organisation is what the sign-in
/// establishes. Usernames are therefore unique system-wide.
/// </summary>
public interface IUserRepository
{
    Task<User?> FindActiveByUsernameAsync(string username, CancellationToken cancellationToken);

    Task<IReadOnlyList<User>> ListActiveAsync(CancellationToken cancellationToken);

    /// <summary>Tenant-scoped, unlike the sign-in lookups: a user of another organisation is not found (D-18 assign).</summary>
    Task<User?> GetInOrganisationAsync(Guid userId, CancellationToken cancellationToken);
}
