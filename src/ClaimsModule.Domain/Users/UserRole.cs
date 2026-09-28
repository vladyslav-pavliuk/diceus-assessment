namespace ClaimsModule.Domain.Users;

/// <summary>
/// Hierarchical (FRS §3): each role has every capability of the roles below it. The numeric values
/// define that order and are never persisted.
/// </summary>
public enum UserRole
{
    Handler = 1,
    Supervisor = 2,
    Manager = 3,
}

public static class UserRoleExtensions
{
    public static bool IsAtLeast(this UserRole role, UserRole minimum)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role.");
        }

        if (!Enum.IsDefined(minimum))
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), minimum, "Unknown role.");
        }

        return role >= minimum;
    }

    /// <summary>The FRS §3 role code, used in the JWT role claim.</summary>
    public static string ToCode(this UserRole role) => role switch
    {
        UserRole.Handler => "handler",
        UserRole.Supervisor => "supervisor",
        UserRole.Manager => "manager",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role."),
    };

    public static bool TryParseCode(string? code, out UserRole role)
    {
        foreach (var candidate in Enum.GetValues<UserRole>())
        {
            if (string.Equals(candidate.ToCode(), code, StringComparison.Ordinal))
            {
                role = candidate;
                return true;
            }
        }

        role = default;
        return false;
    }
}
