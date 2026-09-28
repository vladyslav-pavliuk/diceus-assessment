using MediatR;

namespace ClaimsModule.Application.Common.Messaging;

/// <summary>A state change. The UnitOfWorkBehavior runs every command in one transaction.</summary>
public interface ICommand : IRequest;

/// <inheritdoc cref="ICommand"/>
public interface ICommand<out TResponse> : IRequest<TResponse>;

/// <summary>
/// The handler calls <see cref="Abstractions.IUnitOfWork"/> itself, because part of its work (a blob upload) must
/// neither run inside the transaction nor be replayed with it (D-42).
/// </summary>
public interface IHandlesOwnUnitOfWork;
