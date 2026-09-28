using ClaimsModule.Domain.Common;

namespace ClaimsModule.Application.Common.Events;

/// <summary>
/// Handles a domain event inside the command's transaction, just before SaveChanges (CLAUDE.md rule 5).
/// Whatever it stages commits or rolls back together with the state change, so audit rows and state
/// cannot diverge. It must not call external systems.
/// </summary>
public interface IBeforeCommitHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Handles a domain event after the transaction has committed (CLAUDE.md rule 5), for side effects
/// that must never see uncommitted rows, such as enqueueing a Hangfire job (Phase 4). A failure here
/// cannot undo the commit, so it is logged and the command still succeeds; the GL sweeper (D-15)
/// covers a lost enqueue.
/// </summary>
public interface IAfterCommitHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>Runs the registered handlers of each phase for a batch of domain events. Called by the Unit of Work.</summary>
public interface IDomainEventDispatcher
{
    Task DispatchBeforeCommitAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken);

    Task DispatchAfterCommitAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken);
}
