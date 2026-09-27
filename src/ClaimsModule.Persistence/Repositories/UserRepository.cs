using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Domain.Users;
using ClaimsModule.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Repositories;

/// <summary>
/// Sign-in lookups. They run before the caller has a tenant (the token is what establishes it), so
/// they bypass the tenant filter. EF Core 9's IgnoreQueryFilters removes the soft-delete filter too,
/// so it is re-applied by hand (D-31, D-38 item 6).
/// </summary>
internal sealed class UserRepository(ClaimsDbContext dbContext) : IUserRepository
{
    public Task<User?> FindActiveByUsernameAsync(string username, CancellationToken cancellationToken) =>
        UsersAcrossTenants().SingleOrDefaultAsync(user => user.Username == username && user.IsActive, cancellationToken);

    public async Task<IReadOnlyList<User>> ListActiveAsync(CancellationToken cancellationToken)
    {
        var users = await UsersAcrossTenants().Where(user => user.IsActive).ToListAsync(cancellationToken);

        // Role is stored as text, so order by the role hierarchy in memory (a handful of rows).
        return users.OrderBy(user => user.Role).ThenBy(user => user.Username, StringComparer.Ordinal).ToList();
    }

    private IQueryable<User> UsersAcrossTenants() =>
        dbContext.Users
            .IgnoreQueryFilters()
            .Where(user => !EF.Property<bool>(user, ShadowColumns.IsDeleted))
            .AsNoTracking();
}
