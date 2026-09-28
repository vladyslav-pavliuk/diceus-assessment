using ClaimsModule.API.Contracts.Auth;
using ClaimsModule.Application.Abstractions.Auth;
using ClaimsModule.Application.Users;
using ClaimsModule.Application.Users.Queries.GetUserByUsername;
using ClaimsModule.Application.Users.Queries.ListDemoUsers;
using ClaimsModule.Infrastructure.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ClaimsModule.API.Controllers;

/// <summary>
/// Mock authentication (D-16): 404 unless Auth:DevTokensEnabled. Signing the token is infrastructure, not a business
/// command, so it bypasses MediatR (D-08).
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(ISender sender, ITokenService tokenService, IOptions<AuthOptions> authOptions)
    : ControllerBase
{
    [HttpPost("dev-token")]
    [ProducesResponseType<DevTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DevTokenResponse>> IssueDevToken(DevTokenRequest request, CancellationToken cancellationToken)
    {
        if (!authOptions.Value.DevTokensEnabled)
        {
            return NotFound();
        }

        var user = await sender.Send(new GetUserByUsernameQuery(request.Username!), cancellationToken);
        if (user is null)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unknown or inactive user.");
        }

        var token = tokenService.IssueToken(user);
        return Ok(new DevTokenResponse(token.AccessToken, "Bearer", token.ExpiresAt, user));
    }

    [HttpGet("users")]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> ListUsers(CancellationToken cancellationToken)
    {
        if (!authOptions.Value.DevTokensEnabled)
        {
            return NotFound();
        }

        return Ok(await sender.Send(new ListDemoUsersQuery(), cancellationToken));
    }
}
