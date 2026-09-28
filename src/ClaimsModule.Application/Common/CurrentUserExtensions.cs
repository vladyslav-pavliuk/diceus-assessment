using ClaimsModule.Application.Abstractions;
using ClaimsModule.Domain.Users;

namespace ClaimsModule.Application.Common;

internal static class CurrentUserExtensions
{
    /// <summary>
    /// The caller as the domain sees them: who (self-approval, retraction, audit) and with which role.
    /// Every business endpoint requires a valid token (fallback policy), so a missing claim is a bug.
    /// </summary>
    public static Actor ToActor(this ICurrentUser currentUser) =>
        currentUser is { UserId: { } userId, Role: { } role }
            ? new Actor(userId, role)
            : throw new InvalidOperationException("The request has no authenticated user with a role.");
}
