namespace ClaimsModule.Domain.Common;

/// <summary>
/// An entity that owns a consistency boundary and records what happened to it as domain events.
/// The Unit of Work reads <see cref="DomainEvents"/> after the handler has run: audit rows are
/// written before commit, background jobs are enqueued after commit (CLAUDE.md rule 5).
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot()
    {
    }

    protected AggregateRoot(Guid id)
        : base(id)
    {
    }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
