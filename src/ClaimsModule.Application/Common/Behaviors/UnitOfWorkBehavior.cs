using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Common.Messaging;
using MediatR;

namespace ClaimsModule.Application.Common.Behaviors;

/// <summary>
/// Innermost pipeline behaviour: runs every command handler as one unit of work (CLAUDE.md rule 4).
/// The handler only changes aggregates; <see cref="IUnitOfWork"/> dispatches the before-commit events
/// (audit rows), saves, commits, and then dispatches the after-commit events. Queries pass straight
/// through: they never open a transaction.
/// Validation runs before this behaviour, so an invalid request never opens a transaction.
/// </summary>
internal sealed class UnitOfWorkBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not (ICommand or ICommand<TResponse>))
        {
            return next(cancellationToken);
        }

        // A transient database failure replays the whole unit, including the handler (ARCHITECTURE-PLAN §4).
        return unitOfWork.ExecuteInTransactionAsync(token => next(token), cancellationToken);
    }
}
