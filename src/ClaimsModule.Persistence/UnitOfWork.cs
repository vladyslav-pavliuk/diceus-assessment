using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Common.Events;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Persistence;

/// <summary>
/// One command = one database transaction (CLAUDE.md rule 4). The whole unit runs inside the EF Core
/// execution strategy, because retry-on-failure (D-36: serverless Azure SQL resuming) does not allow a
/// user transaction outside it: a transient failure replays the whole unit, starting from a clean
/// change tracker.
/// <para>
/// Commit sequence (CLAUDE.md rule 5, ARCHITECTURE-PLAN §4):
/// run the handler → dispatch before-commit domain events (audit rows, same transaction) →
/// SaveChanges → COMMIT → dispatch after-commit domain events (Hangfire enqueue, Phase 4).
/// The after-commit phase runs outside the replayed block, so a retry never enqueues twice.
/// </para>
/// <para>
/// Lost races surface here: a RowVer mismatch (DbUpdateConcurrencyException → 409 in the middleware) or, when
/// the loser's first conflicting statement is an INSERT, a duplicate key on one of the unique indexes that guard
/// the aggregate (ChangeSequence, IdempotencyKey, one pending transaction per component). Both mean "another
/// request changed this claim first", so the duplicate key becomes a <see cref="ConflictException"/> (409) too,
/// not a 500 (ARCHITECTURE-PLAN §6.1 M2).
/// </para>
/// </summary>
internal sealed class UnitOfWork(ClaimsDbContext dbContext, IDomainEventDispatcher dispatcher, ILogger<UnitOfWork> logger) : IUnitOfWork
{
    private const int PrimaryKeyViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        IReadOnlyList<IDomainEvent> committedEvents = [];

        var result = await strategy.ExecuteAsync(
            async token =>
            {
                // A replayed attempt must not see entities or events of the failed one.
                dbContext.ChangeTracker.Clear();

                await using var transaction = await dbContext.Database.BeginTransactionAsync(token);
                var result = await operation(token);

                var events = await DispatchBeforeCommitAsync(token);
                await SaveChangesAsync(token);
                await transaction.CommitAsync(token);

                committedEvents = events;
                return result;
            },
            cancellationToken);

        await dispatcher.DispatchAfterCommitAsync(committedEvents, cancellationToken);
        return result;
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: PrimaryKeyViolation or UniqueIndexViolation })
        {
            logger.LogWarning(exception.InnerException, "A concurrent request changed the same data first (duplicate key)");
            throw new ConflictException("The resource was changed by another request. Reload it and try again.");
        }
    }

    /// <summary>
    /// Takes the events raised by every tracked aggregate and runs the before-commit handlers. Repeats
    /// until no new events appear, in case a handler causes another aggregate to raise one.
    /// </summary>
    private async Task<IReadOnlyList<IDomainEvent>> DispatchBeforeCommitAsync(CancellationToken cancellationToken)
    {
        var dispatched = new List<IDomainEvent>();
        while (true)
        {
            var pending = dbContext.ChangeTracker.Entries<AggregateRoot>()
                .SelectMany(entry =>
                {
                    var events = entry.Entity.DomainEvents.ToList();
                    entry.Entity.ClearDomainEvents();
                    return events;
                })
                .ToList();

            if (pending.Count == 0)
            {
                return dispatched;
            }

            await dispatcher.DispatchBeforeCommitAsync(pending, cancellationToken);
            dispatched.AddRange(pending);
        }
    }
}
