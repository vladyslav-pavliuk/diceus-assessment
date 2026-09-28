using System.Security.Claims;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Auth;
using ClaimsModule.Domain.Users;

namespace ClaimsModule.API.Auth;

internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal =>
        httpContextAccessor.HttpContext?.User is { Identity.IsAuthenticated: true } user ? user : null;

    public bool IsAuthenticated => Principal is not null;

    public Guid? UserId => ReadGuid(AppClaimTypes.Subject);

    public string? DisplayName => Principal?.FindFirstValue(AppClaimTypes.Name);

    public UserRole? Role =>
        UserRoleExtensions.TryParseCode(Principal?.FindFirstValue(AppClaimTypes.Role), out var role) ? role : null;

    public Guid? OrganisationId => ReadGuid(AppClaimTypes.Organisation);

    private Guid? ReadGuid(string claimType) =>
        Guid.TryParse(Principal?.FindFirstValue(claimType), out var value) ? value : null;
}
