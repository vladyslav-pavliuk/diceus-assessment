using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Common.Events;
using ClaimsModule.Domain.Common;
using Microsoft.EntityFrameworkCore;

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
/// </summary>
internal sealed class UnitOfWork(ClaimsDbContext dbContext, IDomainEventDispatcher dispatcher) : IUnitOfWork
{
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
                await dbContext.SaveChangesAsync(token);
                await transaction.CommitAsync(token);

                committedEvents = events;
                return result;
            },
            cancellationToken);

        await dispatcher.DispatchAfterCommitAsync(committedEvents, cancellationToken);
        return result;
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
