using ClaimsModule.Domain.Users;
using Microsoft.AspNetCore.Authorization;

namespace ClaimsModule.API.Auth;

/// <summary>
/// Each policy admits its role and every role above it. They return 403 when a role can never act (D-25);
/// data-dependent authority is checked again in the domain.
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

        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    }

    private static IEnumerable<string> RoleCodesAtLeast(UserRole minimum) =>
        Enum.GetValues<UserRole>().Where(role => role.IsAtLeast(minimum)).Select(role => role.ToCode());
}
