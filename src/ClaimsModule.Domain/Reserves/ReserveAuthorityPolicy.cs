using ClaimsModule.Domain.Users;

namespace ClaimsModule.Domain.Reserves;

/// <summary>
/// FRS §6.3 / BR-R-02: the approval tier depends on the amount of the individual transaction,
/// not on the running total. The absolute value is used, so a large release of reserves needs the
/// same authority as a large increase (D-05).
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

    /// <summary>Roles are hierarchical (FRS §3), so a manager can approve a supervisor-tier transaction.</summary>
    public static bool CanApprove(UserRole role, ApprovalAuthority required) => required switch
    {
        ApprovalAuthority.Auto => true,
        ApprovalAuthority.Supervisor => role.IsAtLeast(UserRole.Supervisor),
        ApprovalAuthority.Manager => role.IsAtLeast(UserRole.Manager),
        _ => throw new ArgumentOutOfRangeException(nameof(required), required, "Unknown approval authority."),
    };
}
