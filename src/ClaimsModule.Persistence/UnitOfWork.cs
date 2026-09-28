using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Common.Events;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Persistence;

/// <summary>
/// Runs inside the execution strategy, because retry-on-failure does not allow a user transaction outside it (D-36).
/// After-commit handlers run outside the replayed block, so a retry never enqueues twice.
/// <para>
/// A lost race surfaces either as a RowVer mismatch or as a duplicate key on an index that guards the aggregate. Both mean
/// "another request changed this claim first", so both become 409.
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

    /// <summary>Repeats until no new events appear, in case a handler makes another aggregate raise one.</summary>
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
