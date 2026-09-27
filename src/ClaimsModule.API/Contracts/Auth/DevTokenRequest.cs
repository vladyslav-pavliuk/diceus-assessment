namespace ClaimsModule.API.Contracts.Auth;

/// <summary>Username may be null here; the GetUserByUsernameQuery validator rejects it with a 422.</summary>
public sealed record DevTokenRequest(string? Username);
