using MediatR;

namespace ClaimsModule.Application.Common.Messaging;

/// <summary>Read-only; never opens a unit of work.</summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;
