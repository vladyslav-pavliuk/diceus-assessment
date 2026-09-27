using ClaimsModule.Application.Users;

namespace ClaimsModule.API.Contracts.Auth;

public sealed record DevTokenResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt, UserDto User);
