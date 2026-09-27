using MediatR;

namespace ClaimsModule.Application.Common.Messaging;

/// <summary>
/// A read-only request (CQRS read side). Named GetNounQuery or ListNounsQuery (FRS §15.3).
/// A query never changes state and never opens a unit of work.
/// </summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;
