using AutoMapper;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Common.Messaging;
using MediatR;

namespace ClaimsModule.Application.Users.Queries.GetUserByUsername;

/// <summary>Active users only, for the dev-token sign-in (D-16).</summary>
public sealed record GetUserByUsernameQuery(string Username) : IQuery<UserDto?>;

internal sealed class GetUserByUsernameQueryHandler(IUserRepository users, IMapper mapper)
    : IRequestHandler<GetUserByUsernameQuery, UserDto?>
{
    public async Task<UserDto?> Handle(GetUserByUsernameQuery request, CancellationToken cancellationToken)
    {
        var user = await users.FindActiveByUsernameAsync(request.Username.Trim(), cancellationToken);
        return user is null ? null : mapper.Map<UserDto>(user);
    }
}
