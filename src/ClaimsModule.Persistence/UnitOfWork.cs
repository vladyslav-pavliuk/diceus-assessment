using ClaimsModule.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

/// <summary>
/// One command = one database transaction (CLAUDE.md rule 4). The whole unit runs inside the EF Core
/// execution strategy, because retry-on-failure (D-36: serverless Azure SQL resuming) does not allow a
/// user transaction outside it: a transient failure replays the whole unit, starting from a clean
/// change tracker.
/// <para>
/// Phase 3 adds domain-event dispatch here: before-commit handlers (audit rows) just before
/// SaveChanges, after-commit handlers (Hangfire enqueue) after the commit (CLAUDE.md rule 5).
/// </para>
/// </summary>
internal sealed class UnitOfWork(ClaimsDbContext dbContext) : IUnitOfWork
{
    public Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return strategy.ExecuteAsync(
            async token =>
            {
                // A replayed attempt must not see entities tracked by the failed one.
                dbContext.ChangeTracker.Clear();

                await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
                var result = await operation(token);
                await dbContext.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
                return result;
            },
            cancellationToken);
    }
}
