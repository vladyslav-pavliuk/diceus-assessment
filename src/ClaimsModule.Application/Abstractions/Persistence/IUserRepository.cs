using ClaimsModule.Domain.Users;

namespace ClaimsModule.Application.Abstractions.Persistence;

/// <summary>
/// Read access to seeded users for the mock sign-in (D-16).
/// Deliberately not tenant-scoped: at sign-in the caller has no organisation yet, and the
/// organisation is what the sign-in establishes. Usernames are therefore unique system-wide.
/// </summary>
public interface IUserRepository
{
    Task<User?> FindActiveByUsernameAsync(string username, CancellationToken cancellationToken);

    Task<IReadOnlyList<User>> ListActiveAsync(CancellationToken cancellationToken);
}
