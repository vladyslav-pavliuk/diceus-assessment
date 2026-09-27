namespace ClaimsModule.Application.Abstractions.Auth;

/// <summary>
/// JWT claim names shared by the token issuer (Infrastructure) and the token reader (API).
/// Short JWT names are kept as-is; inbound claim mapping is switched off in the API.
/// </summary>
public static class AppClaimTypes
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Role = "role";
    public const string Organisation = "org";
}
