namespace ClaimsModule.Application.Abstractions.Auth;

/// <summary>Short JWT claim names; inbound claim mapping is switched off in the API.</summary>
public static class AppClaimTypes
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Role = "role";
    public const string Organisation = "org";
}
