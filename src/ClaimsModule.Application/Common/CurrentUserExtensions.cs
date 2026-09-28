using ClaimsModule.Application.Abstractions;
using ClaimsModule.Domain.Users;

namespace ClaimsModule.Application.Common;

internal static class CurrentUserExtensions
{
    /// <summary>Every business endpoint requires a valid token, so a missing user or role is a bug, not a 401.</summary>
    public static Actor ToActor(this ICurrentUser currentUser) =>
        currentUser is { UserId: { } userId, Role: { } role }
            ? new Actor(userId, role)
            : throw new InvalidOperationException("The request has no authenticated user with a role.");
}
