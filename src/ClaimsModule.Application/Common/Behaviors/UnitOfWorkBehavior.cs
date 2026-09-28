using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Common.Messaging;
using MediatR;

namespace ClaimsModule.Application.Common.Behaviors;

/// <summary>
/// Wraps every command in one unit of work; queries pass through. A command marked
/// <see cref="IHandlesOwnUnitOfWork"/> runs its unit of work itself (D-42).
/// </summary>
internal sealed class UnitOfWorkBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not (ICommand or ICommand<TResponse>) || request is IHandlesOwnUnitOfWork)
        {
            return next(cancellationToken);
        }

        // A transient database failure replays the whole unit, including the handler.
        return unitOfWork.ExecuteInTransactionAsync(token => next(token), cancellationToken);
    }
}
