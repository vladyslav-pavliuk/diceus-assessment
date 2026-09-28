using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Validation;
using ClaimsModule.Domain.Users;
using FluentValidation;
using MediatR;

namespace ClaimsModule.Application.Users.Queries.ListUsers;

/// <summary>Active users of the caller's organisation. <see cref="Role"/> matches exactly, not hierarchically.</summary>
public sealed record ListUsersQuery(UserRole? Role = null) : IQuery<IReadOnlyList<UserDto>>;

internal sealed class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator()
    {
        RuleFor(query => query.Role).IsInEnum().WithMessage(RequestMessages.InvalidRole);
    }
}

internal sealed class ListUsersQueryHandler(IUserQueries users) : IRequestHandler<ListUsersQuery, IReadOnlyList<UserDto>>
{
    public Task<IReadOnlyList<UserDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken) =>
        users.ListActiveAsync(request.Role, cancellationToken);
}
