using System.Security.Claims;
using ClaimsModule.Application.Abstractions.Auth;
using ClaimsModule.Application.Users;
using ClaimsModule.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ClaimsModule.Infrastructure.Auth;

/// <summary>HS256-signed JWTs for seeded users (D-16).</summary>
internal sealed class JwtTokenService(IOptions<AuthOptions> options, TimeProvider timeProvider) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };

    public IssuedToken IssueToken(UserDto user)
    {
        var settings = options.Value;
        var issuedAt = timeProvider.GetUtcNow();
        var expiresAt = issuedAt + settings.TokenLifetime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(AppClaimTypes.Subject, user.Id.ToString()),
                new Claim(AppClaimTypes.Name, user.DisplayName),
                new Claim(AppClaimTypes.Role, user.Role.ToCode()),
                new Claim(AppClaimTypes.Organisation, user.OrganisationId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
            SigningCredentials = new SigningCredentials(settings.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        };

        return new IssuedToken(_handler.CreateToken(descriptor), expiresAt);
    }
}
