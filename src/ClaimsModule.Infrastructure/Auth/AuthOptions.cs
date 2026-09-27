using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ClaimsModule.Infrastructure.Auth;

/// <summary>
/// Configuration section "Auth" (D-16). In Azure, SigningKey comes from Key Vault through a
/// Container Apps secret reference (env var Auth__SigningKey); it is never committed for
/// non-development environments.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>HS256 requires a key of at least 256 bits.</summary>
    public const int MinimumSigningKeyLength = 32;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromHours(8);

    /// <summary>
    /// Enables POST /api/auth/dev-token and GET /api/auth/users. On in the demo deployment so
    /// reviewers can switch roles; a documented non-production shortcut (D-16).
    /// </summary>
    public bool DevTokensEnabled { get; init; }

    /// <summary>The HS256 key used both to sign (token service) and to validate (JwtBearer) tokens.</summary>
    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
