using System.Collections.Concurrent;
using System.Reflection;
using ClaimsModule.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Application.Common.Events;

/// <summary>
/// Resolves the handlers of each event's runtime type from the request scope and runs them in order.
/// Two explicit handler interfaces are used instead of MediatR notifications, because MediatR has no
/// notion of "before" and "after" commit, and the phase is the important fact about a handler (D-40).
/// </summary>
internal sealed class DomainEventDispatcher(IServiceProvider services, ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<(Type Handler, Type Event), MethodInfo> HandleMethods = new();

    public async Task DispatchBeforeCommitAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            foreach (var (handler, handle) in HandlersFor(typeof(IBeforeCommitHandler<>), domainEvent))
            {
                // Any failure rolls the whole unit back, audit rows included.
                await InvokeAsync(handle, handler, domainEvent, cancellationToken);
            }
        }
    }

    public async Task DispatchAfterCommitAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            foreach (var (handler, handle) in HandlersFor(typeof(IAfterCommitHandler<>), domainEvent))
            {
                try
                {
                    await InvokeAsync(handle, handler, domainEvent, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // The transaction is committed; failing the request now would make the client retry a
                    // change that already happened. Each handler is independent, so the others still run.
                    // The GL sweeper re-enqueues stranded postings (D-15).
                    logger.LogError(
                        exception, "After-commit handler {Handler} failed for {DomainEvent}", handler.GetType().Name, domainEvent.GetType().Name);
                }
            }
        }
    }

    private IEnumerable<(object Handler, MethodInfo Handle)> HandlersFor(Type openHandlerType, IDomainEvent domainEvent)
    {
        var handlerType = openHandlerType.MakeGenericType(domainEvent.GetType());
        var handle = HandleMethods.GetOrAdd(
            (openHandlerType, domainEvent.GetType()),
            _ => handlerType.GetMethod(nameof(IBeforeCommitHandler<IDomainEvent>.HandleAsync))!);

        return services.GetServices(handlerType).Select(handler => (handler!, handle));
    }

    // DoNotWrapExceptions: a handler's exception must reach the error middleware as itself (a 422 stays
    // a 422), not wrapped in a TargetInvocationException.
    private static Task InvokeAsync(MethodInfo handle, object handler, IDomainEvent domainEvent, CancellationToken cancellationToken) =>
        (Task)handle.Invoke(handler, BindingFlags.DoNotWrapExceptions, binder: null, [domainEvent, cancellationToken], culture: null)!;
}
