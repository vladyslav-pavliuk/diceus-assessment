using ClaimsModule.Domain.Users;
using Microsoft.AspNetCore.Authorization;

namespace ClaimsModule.API.Auth;

/// <summary>
/// Role policies. Roles are hierarchical (FRS §3), so each policy admits its role and every role
/// above it: "Handler" = any role, "Supervisor" = supervisor or manager, "Manager" = manager only.
/// Policies gate endpoints (403 when the role can never perform the action, D-25); data-dependent
/// authority checks happen again in the domain.
/// </summary>
public static class AuthorizationPolicies
{
    public const string Handler = nameof(UserRole.Handler);
    public const string Supervisor = nameof(UserRole.Supervisor);
    public const string Manager = nameof(UserRole.Manager);

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(Handler, policy => policy.RequireRole(RoleCodesAtLeast(UserRole.Handler)));
        options.AddPolicy(Supervisor, policy => policy.RequireRole(RoleCodesAtLeast(UserRole.Supervisor)));
        options.AddPolicy(Manager, policy => policy.RequireRole(RoleCodesAtLeast(UserRole.Manager)));

        // Every endpoint requires a valid token unless it opts out with [AllowAnonymous].
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    }

    private static IEnumerable<string> RoleCodesAtLeast(UserRole minimum) =>
        Enum.GetValues<UserRole>().Where(role => role.IsAtLeast(minimum)).Select(role => role.ToCode());
}
