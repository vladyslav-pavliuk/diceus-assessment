using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Repositories;

internal sealed class UserRepository(ClaimsDbContext dbContext) : IUserRepository
{
    public Task<User?> FindActiveByUsernameAsync(string username, CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Username == username && user.IsActive, cancellationToken);

    public async Task<IReadOnlyList<User>> ListActiveAsync(CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive)
            .ToListAsync(cancellationToken);

        // Role is stored as text, so order by the role hierarchy in memory (a handful of rows).
        return users.OrderBy(user => user.Role).ThenBy(user => user.Username, StringComparer.Ordinal).ToList();
    }
}
