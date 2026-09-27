using AutoMapper;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common.Messaging;
using MediatR;

namespace ClaimsModule.Application.Users.Queries.ListDemoUsers;

/// <summary>Lists the active seeded users for the frontend role switcher (FRS §11.4, D-08).</summary>
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
