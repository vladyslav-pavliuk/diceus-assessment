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
/// Mock authentication (D-16). Both endpoints exist only while Auth:DevTokensEnabled is true;
/// otherwise they answer 404 as if they were not there. The user lookup is a MediatR query;
/// signing the token is infrastructure (ITokenService), not a business command (D-08).
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(ISender sender, ITokenService tokenService, IOptions<AuthOptions> authOptions)
    : ControllerBase
{
    /// <summary>Issues a signed JWT for a seeded user.</summary>
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

    /// <summary>Lists the seeded users for the role switcher (FRS §11.4).</summary>
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
