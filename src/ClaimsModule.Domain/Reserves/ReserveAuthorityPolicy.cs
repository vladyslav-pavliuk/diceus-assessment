using ClaimsModule.Domain.Users;

namespace ClaimsModule.Domain.Reserves;

/// <summary>
/// BR-R-02: the tier depends on the |amount| of the single transaction, not the running total, so a
/// large release needs the same authority as a large increase (D-05).
/// </summary>
public static class ReserveAuthorityPolicy
{
    public const decimal AutoApprovalLimit = 10_000m;
    public const decimal SupervisorLimit = 100_000m;

    public static ApprovalAuthority RequiredAuthorityFor(decimal amount)
    {
        var magnitude = Math.Abs(amount);
        if (magnitude <= AutoApprovalLimit)
        {
            return ApprovalAuthority.Auto;
        }

        return magnitude <= SupervisorLimit ? ApprovalAuthority.Supervisor : ApprovalAuthority.Manager;
    }

    public static bool CanApprove(UserRole role, ApprovalAuthority required) => required switch
    {
        ApprovalAuthority.Auto => true,
        ApprovalAuthority.Supervisor => role.IsAtLeast(UserRole.Supervisor),
        ApprovalAuthority.Manager => role.IsAtLeast(UserRole.Manager),
        _ => throw new ArgumentOutOfRangeException(nameof(required), required, "Unknown approval authority."),
    };
}
