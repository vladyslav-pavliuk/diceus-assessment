using AutoMapper;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common.Messaging;
using MediatR;

namespace ClaimsModule.Application.Users.Queries.ListDemoUsers;

/// <summary>For the sign-in role switcher, so not tenant-scoped (D-16).</summary>
public sealed record ListDemoUsersQuery : IQuery<IReadOnlyList<UserDto>>;

internal sealed class ListDemoUsersQueryHandler(IUserRepository users, IMapper mapper)
    : IRequestHandler<ListDemoUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<IReadOnlyList<UserDto>> Handle(ListDemoUsersQuery request, CancellationToken cancellationToken)
    {
        var activeUsers = await users.ListActiveAsync(cancellationToken);
        return mapper.Map<IReadOnlyList<UserDto>>(activeUsers);
    }
}
