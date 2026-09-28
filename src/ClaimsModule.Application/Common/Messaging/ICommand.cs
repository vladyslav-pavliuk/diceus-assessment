using MediatR;

namespace ClaimsModule.Application.Common.Messaging;

/// <summary>
/// A state-changing request (CQRS write side). Named VerbNounCommand (FRS §15.3).
/// From Phase 3 the UnitOfWorkBehavior wraps every command in one transaction; queries skip it.
/// </summary>
public interface ICommand : IRequest;

/// <inheritdoc cref="ICommand"/>
public interface ICommand<out TResponse> : IRequest<TResponse>;

/// <summary>
/// A command whose handler calls <see cref="Abstractions.IUnitOfWork"/> itself, because it also does work outside the
/// database that must neither run inside the transaction nor be replayed with it (D-42): a document upload writes the
/// blob first and the metadata second. The UnitOfWorkBehavior lets such a command through; the database part is still
/// exactly one unit of work, with the same before/after-commit event dispatch.
/// </summary>
public interface IHandlesOwnUnitOfWork;
