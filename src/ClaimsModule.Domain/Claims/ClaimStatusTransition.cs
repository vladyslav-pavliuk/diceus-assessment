using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Users;

namespace ClaimsModule.Domain.Claims;

/// <summary>
/// One allowed status transition (FRS §4.2, D-09). The rows are seeded into the
/// ClaimStatusTransitions table and served by GET /api/reference/claim-statuses, and the aggregate
/// enforces exactly those rows: the rule is data, not a switch statement.
/// </summary>
public sealed class ClaimStatusTransition : Entity
{
    private ClaimStatusTransition()
    {
    }

    private ClaimStatusTransition(Guid id)
        : base(id)
    {
    }

    public ClaimStatus FromStatus { get; private set; }

    public ClaimStatus ToStatus { get; private set; }

    /// <summary>The lowest role allowed to request it (compared hierarchically). Null for system-only rows.</summary>
    public UserRole? MinimumRole { get; private set; }

    public bool RequiresReason { get; private set; }

    /// <summary>Applied by the system inside another transition (Reopened → Open), never requested.</summary>
    public bool IsSystemOnly { get; private set; }

    public static ClaimStatusTransition Create(
        ClaimStatus fromStatus, ClaimStatus toStatus, UserRole? minimumRole, bool requiresReason, bool isSystemOnly)
    {
        if (isSystemOnly == minimumRole.HasValue)
        {
            throw new ArgumentException("A transition has a minimum role exactly when it is not system-only.", nameof(minimumRole));
        }

        return new ClaimStatusTransition(SequentialGuid.NewGuid())
        {
            FromStatus = fromStatus,
            ToStatus = toStatus,
            MinimumRole = minimumRole,
            RequiresReason = requiresReason,
            IsSystemOnly = isSystemOnly,
        };
    }

    /// <summary>
    /// The FRS §4.2 table as decided in D-09. Declared once, here: Persistence seeds these rows
    /// (HasData), and domain tests run against them.
    /// </summary>
    public static IReadOnlyList<ClaimStatusTransition> FrsDefaults() =>
    [
        Create(ClaimStatus.Draft, ClaimStatus.Open, UserRole.Handler, requiresReason: false, isSystemOnly: false),
        Create(ClaimStatus.Open, ClaimStatus.UnderInvestigation, UserRole.Handler, requiresReason: false, isSystemOnly: false),
        Create(ClaimStatus.Open, ClaimStatus.PendingPayment, UserRole.Handler, requiresReason: false, isSystemOnly: false),
        Create(ClaimStatus.Open, ClaimStatus.Closed, UserRole.Handler, requiresReason: true, isSystemOnly: false),
        Create(ClaimStatus.Open, ClaimStatus.Withdrawn, UserRole.Handler, requiresReason: true, isSystemOnly: false),
        Create(ClaimStatus.UnderInvestigation, ClaimStatus.Open, UserRole.Handler, requiresReason: false, isSystemOnly: false),
        Create(ClaimStatus.UnderInvestigation, ClaimStatus.PendingPayment, UserRole.Handler, requiresReason: false, isSystemOnly: false),
        Create(ClaimStatus.UnderInvestigation, ClaimStatus.Closed, UserRole.Handler, requiresReason: true, isSystemOnly: false),
        Create(ClaimStatus.UnderInvestigation, ClaimStatus.Withdrawn, UserRole.Handler, requiresReason: true, isSystemOnly: false),
        Create(ClaimStatus.PendingPayment, ClaimStatus.Closed, UserRole.Handler, requiresReason: true, isSystemOnly: false),
        Create(ClaimStatus.Closed, ClaimStatus.Reopened, UserRole.Supervisor, requiresReason: true, isSystemOnly: false),
        Create(ClaimStatus.Reopened, ClaimStatus.Open, minimumRole: null, requiresReason: false, isSystemOnly: true),
    ];
}

/// <summary>The loaded set of <see cref="ClaimStatusTransition"/> rows, as the aggregate consults it.</summary>
public sealed class StatusTransitionTable
{
    private readonly IReadOnlyList<ClaimStatusTransition> _transitions;

    public StatusTransitionTable(IEnumerable<ClaimStatusTransition> transitions)
    {
        _transitions = transitions.ToList();
        var duplicate = _transitions.GroupBy(t => (t.FromStatus, t.ToStatus)).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate transition {duplicate.Key.FromStatus} → {duplicate.Key.ToStatus}.", nameof(transitions));
        }
    }

    public ClaimStatusTransition? Find(ClaimStatus from, ClaimStatus to) =>
        _transitions.SingleOrDefault(t => t.FromStatus == from && t.ToStatus == to);

    /// <summary>The statuses a user may request from <paramref name="from"/> (system-only rows excluded).</summary>
    public IReadOnlyList<ClaimStatus> ValidNextStatuses(ClaimStatus from) =>
        _transitions.Where(t => t.FromStatus == from && !t.IsSystemOnly).Select(t => t.ToStatus).Order().ToList();
}
