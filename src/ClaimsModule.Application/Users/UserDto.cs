using ClaimsModule.Domain.Users;

namespace ClaimsModule.Application.Users;

public sealed record UserDto(Guid Id, string Username, string DisplayName, UserRole Role, Guid OrganisationId);
