using MediatR;

namespace ClaimsModule.Application.Common.Messaging;

/// <summary>
/// A state-changing request (CQRS write side). Named VerbNounCommand (FRS §15.3).
/// From Phase 2 the UnitOfWorkBehavior wraps every command in one transaction; queries skip it.
/// </summary>
public interface ICommand : IRequest;

/// <inheritdoc cref="ICommand"/>
public interface ICommand<out TResponse> : IRequest<TResponse>;
