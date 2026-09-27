namespace ClaimsModule.Domain.Users;

/// <summary>
/// The three roles from FRS §3. They are hierarchical: each role has all the capabilities of the
/// roles below it ("All handler capabilities +", "All supervisor capabilities +").
/// The numeric values define that order; they are never persisted (enums are stored as NVARCHAR(50)).
/// </summary>
public enum UserRole
{
    Handler = 1,
    Supervisor = 2,
    Manager = 3,
}

public static class UserRoleExtensions
{
    /// <summary>
    /// True when <paramref name="role"/> has at least the capabilities of <paramref name="minimum"/>.
    /// Used by the API role policies and by the status-transition MinimumRole check (D-09).
    /// </summary>
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

    /// <summary>The role code from FRS §3 ("handler", "supervisor", "manager"), used in the JWT role claim.</summary>
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
