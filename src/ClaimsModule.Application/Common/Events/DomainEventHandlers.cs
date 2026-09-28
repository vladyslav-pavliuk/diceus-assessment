using ClaimsModule.Domain.Common;

namespace ClaimsModule.Application.Common.Events;

/// <summary>
/// Runs inside the command's transaction, so what it stages commits or rolls back with the state change.
/// Must not call external systems.
/// </summary>
public interface IBeforeCommitHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Runs after commit, for side effects that must never see uncommitted rows. A failure is logged and the
/// command still succeeds; the GL sweeper covers a lost enqueue (D-15).
/// </summary>
public interface IAfterCommitHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

public interface IDomainEventDispatcher
{
    Task DispatchBeforeCommitAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken);

    Task DispatchAfterCommitAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken);
}
