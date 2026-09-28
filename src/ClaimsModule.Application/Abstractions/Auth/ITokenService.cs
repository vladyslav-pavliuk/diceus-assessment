using ClaimsModule.Application.Users;

namespace ClaimsModule.Application.Abstractions.Auth;

/// <summary>Mock authentication (D-16). Infrastructure rather than a business command, so not MediatR (D-08).</summary>
public interface ITokenService
{
    IssuedToken IssueToken(UserDto user);
}

public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt);
