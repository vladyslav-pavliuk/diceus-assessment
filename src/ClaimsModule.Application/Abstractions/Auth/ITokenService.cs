using ClaimsModule.Application.Users;

namespace ClaimsModule.Application.Abstractions.Auth;

/// <summary>
/// Issues signed access tokens for the mock authentication (D-16). Token issuance is
/// infrastructure, not a business command, so it does not go through MediatR (D-08).
/// </summary>
public interface ITokenService
{
    IssuedToken IssueToken(UserDto user);
}

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt);
