using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ClaimsModule.Infrastructure.Auth;

/// <summary>In Azure, SigningKey comes from Key Vault through a Container Apps secret reference (D-16).</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public const int MinimumSigningKeyLength = 32;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromHours(8);

    /// <summary>A documented non-production shortcut, on in the demo so reviewers can switch roles (D-16).</summary>
    public bool DevTokensEnabled { get; init; }

    /// <summary>Used both to sign and to validate tokens.</summary>
    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
