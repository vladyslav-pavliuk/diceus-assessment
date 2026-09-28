namespace ClaimsModule.API.Contracts.Auth;

/// <summary>Nullable, so the validator rather than binding rejects a missing username.</summary>
public sealed record DevTokenRequest(string? Username);
