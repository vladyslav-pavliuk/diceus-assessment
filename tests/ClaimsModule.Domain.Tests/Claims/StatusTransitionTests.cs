using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Claims.Events;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Documents;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Domain.Users;
using static ClaimsModule.Domain.Tests.TestData;

namespace ClaimsModule.Domain.Tests.Claims;

public sealed class StatusTransitionTests
{
    // FRS §4.2 as rows (D-09): (from, to, minimum role, requires reason, system only).
    public static readonly TheoryData<ClaimStatus, ClaimStatus, UserRole?, bool, bool> FrsTable = new()
    {
        { ClaimStatus.Draft, ClaimStatus.Open, UserRole.Handler, false, false },
        { ClaimStatus.Open, ClaimStatus.UnderInvestigation, UserRole.Handler, false, false },
        { ClaimStatus.Open, ClaimStatus.PendingPayment, UserRole.Handler, false, false },
        { ClaimStatus.Open, ClaimStatus.Closed, UserRole.Handler, true, false },
        { ClaimStatus.Open, ClaimStatus.Withdrawn, UserRole.Handler, true, false },
        { ClaimStatus.UnderInvestigation, ClaimStatus.Open, UserRole.Handler, false, false },
        { ClaimStatus.UnderInvestigation, ClaimStatus.PendingPayment, UserRole.Handler, false, false },
        { ClaimStatus.UnderInvestigation, ClaimStatus.Closed, UserRole.Handler, true, false },
        { ClaimStatus.UnderInvestigation, ClaimStatus.Withdrawn, UserRole.Handler, true, false },
        { ClaimStatus.PendingPayment, ClaimStatus.Closed, UserRole.Handler, true, false },
        { ClaimStatus.Closed, ClaimStatus.Reopened, UserRole.Supervisor, true, false },
        { ClaimStatus.Reopened, ClaimStatus.Open, null, false, true },
    };

    [Theory]
    [MemberData(nameof(FrsTable))]
    public void D_09_Transition_table_matches_FRS_4_2(ClaimStatus from, ClaimStatus to, UserRole? minimumRole, bool requiresReason, bool isSystemOnly)
    {
        var row = Transitions.Find(from, to).ShouldNotBeNull();

        row.MinimumRole.ShouldBe(minimumRole);
        row.RequiresReason.ShouldBe(requiresReason);
        row.IsSystemOnly.ShouldBe(isSystemOnly);
    }

    [Fact]
    public void D_09_Transition_table_has_exactly_the_FRS_rows()
    {
        ClaimStatusTransition.FrsDefaults().Count.ShouldBe(FrsTable.Count);
    }

    public static TheoryData<ClaimStatus, ClaimStatus> UnlistedTransitions()
    {
        var data = new TheoryData<ClaimStatus, ClaimStatus>();
        ClaimStatus[] restingStatuses =
            [ClaimStatus.Draft, ClaimStatus.Open, ClaimStatus.UnderInvestigation, ClaimStatus.PendingPayment, ClaimStatus.Closed, ClaimStatus.Withdrawn];

        foreach (var from in restingStatuses)
        {
            foreach (var to in Enum.GetValues<ClaimStatus>())
            {
                if (Transitions.Find(from, to) is null)
                {
                    data.Add(from, to);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(UnlistedTransitions))]
    public void TR_00_Unlisted_transition_is_rejected(ClaimStatus from, ClaimStatus to)
    {
        var claim = ClaimIn(from);

        var errors = ShouldViolate(() => claim.ChangeStatus(to, "reason", "justification", Manager, Transitions, Now));

        errors[ErrorKeys.StatusTransition][0].ShouldBe($"Transition from {from} to {to} is not permitted."); // VAL-11
        claim.Status.ShouldBe(from);
        claim.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void BR_ST_01_Invalid_transition_lists_valid_next_statuses()
    {
        var errors = ShouldViolate(() => OpenClaim().ChangeStatus(ClaimStatus.Reopened, "reason", null, Manager, Transitions, Now));

        errors[ErrorKeys.StatusTransition].ShouldBe(
        [
            "Transition from Open to Reopened is not permitted.",
            "Valid next statuses: UnderInvestigation, PendingPayment, Closed, Withdrawn.",
        ]);
    }

    [Fact]
    public void BR_ST_01_A_terminal_status_lists_no_valid_next_status()
    {
        ShouldViolate(() => ClaimIn(ClaimStatus.Withdrawn).ChangeStatus(ClaimStatus.Open, null, null, Manager, Transitions, Now))
            [ErrorKeys.StatusTransition][1].ShouldBe("Valid next statuses: none.");
    }

    [Fact]
    public void B_C_06_Draft_to_closed_is_rejected()
    {
        ShouldViolate(() => DraftClaim().ChangeStatus(ClaimStatus.Closed, "Trivial", null, Manager, Transitions, Now))
            [ErrorKeys.StatusTransition][0].ShouldBe("Transition from Draft to Closed is not permitted.");
    }

    [Fact]
    public void TR_01_Draft_to_open()
    {
        var claim = DraftClaim();

        claim.ChangeStatus(ClaimStatus.Open, null, null, Handler, Transitions, Now);

        claim.Status.ShouldBe(ClaimStatus.Open);
    }

    [Fact]
    public void AUD_02_Status_change_raises_event_with_old_and_new_status()
    {
        var claim = DraftClaim();

        claim.ChangeStatus(ClaimStatus.Open, null, null, Handler, Transitions, Now);

        claim.SingleEvent<ClaimStatusChanged>().ShouldBe(new ClaimStatusChanged(claim.Id, ClaimStatus.Draft, ClaimStatus.Open, null));
    }

    [Fact]
    public void BR_C_03_Open_without_active_claimant_is_rejected()
    {
        var claim = DraftClaim(withClaimant: false);

        ShouldViolate(() => claim.ChangeStatus(ClaimStatus.Open, null, null, Handler, Transitions, Now))[ErrorKeys.ClaimParties]
            .ShouldBe(["At least one Claimant party is required to open a claim."]);
        claim.Status.ShouldBe(ClaimStatus.Draft);
    }

    [Fact]
    public void BR_ST_02_Open_with_unresolved_critical_is_rejected_listing_every_condition()
    {
        // No claimant (Critical, BR-C-03) and an unacknowledged BR-C-02 warning (D-19).
        var claim = DraftClaim(withClaimant: false, policy: PolicyCovering(new DateOnly(2020, 1, 1), new DateOnly(2021, 1, 1)));

        var errors = ShouldViolate(() => claim.ChangeStatus(ClaimStatus.Open, null, null, Handler, Transitions, Now));

        errors.Keys.ShouldBe([ErrorKeys.ClaimParties, ErrorKeys.LossDate], ignoreOrder: true);
    }

    [Fact]
    public void BR_ST_02_Open_with_all_conditions_met_succeeds_despite_non_blocking_warnings()
    {
        // BR-C-06 (no policy) and no-risk-object warnings do not block Open (FRS §5.4, D-19).
        var claim = DraftClaim(withPolicy: false, withRiskObject: false);
        claim.ValidationIssues.Count.ShouldBe(2);

        claim.ChangeStatus(ClaimStatus.Open, null, null, Handler, Transitions, Now);

        claim.Status.ShouldBe(ClaimStatus.Open);
    }

    [Fact]
    public void BR_C_02_Unacknowledged_warning_blocks_open()
    {
        var claim = DraftClaim(policy: PolicyCovering(new DateOnly(2020, 1, 1), new DateOnly(2021, 1, 1)));

        ShouldViolate(() => claim.ChangeStatus(ClaimStatus.Open, null, null, Handler, Transitions, Now))[ErrorKeys.LossDate]
            .ShouldBe([DomainMessages.LossDateOutsidePolicyPeriodNotAcknowledged]);
    }

    [Fact]
    public void BR_C_02_Acknowledged_warning_allows_open()
    {
        var claim = DraftClaim(policy: PolicyCovering(new DateOnly(2020, 1, 1), new DateOnly(2021, 1, 1)));
        var warning = claim.ValidationIssues.Single(issue => issue.RuleCode == ValidationRuleCodes.LossDateOutsidePolicyPeriod);

        claim.AcknowledgeValidationIssue(warning.Id, "Late notification; cover confirmed by underwriting.", Handler, Now);
        claim.ChangeStatus(ClaimStatus.Open, null, null, Handler, Transitions, Now);

        claim.Status.ShouldBe(ClaimStatus.Open);
        warning.Status.ShouldBe(IssueStatus.Acknowledged);
        warning.ResolvedByUserId.ShouldBe(Handler.UserId);
    }

    [Fact]
    public void TR_02_Open_to_under_investigation()
    {
        var claim = OpenClaim();

        claim.ChangeStatus(ClaimStatus.UnderInvestigation, null, null, Handler, Transitions, Now);

        claim.Status.ShouldBe(ClaimStatus.UnderInvestigation);
    }

    [Fact]
    public void TR_06_Under_investigation_to_open()
    {
        var claim = ClaimIn(ClaimStatus.UnderInvestigation);

        claim.ChangeStatus(ClaimStatus.Open, null, null, Handler, Transitions, Now);

        claim.Status.ShouldBe(ClaimStatus.Open);
    }

    [Theory]
    [InlineData(ClaimStatus.Open)]
    [InlineData(ClaimStatus.UnderInvestigation)]
    public void TR_03_TR_07_Pending_payment_requires_an_approved_reserve(ClaimStatus from)
    {
        var claim = ClaimIn(from);
        Submit(claim, ReserveComponentType.Indemnity, 50_000m); // pending approval: does not count

        ShouldViolate(() => claim.ChangeStatus(ClaimStatus.PendingPayment, null, null, Handler, Transitions, Now))[ErrorKeys.Reserves]
            .ShouldBe(["At least one approved reserve is required to move a claim to PendingPayment."]);

        Submit(claim, ReserveComponentType.Expense, 2_000m); // auto-approved
        claim.ChangeStatus(ClaimStatus.PendingPayment, null, null, Handler, Transitions, Now);

        claim.Status.ShouldBe(ClaimStatus.PendingPayment);
    }

    [Theory]
    [InlineData(ClaimStatus.Open)]
    [InlineData(ClaimStatus.UnderInvestigation)]
    [InlineData(ClaimStatus.PendingPayment)]
    public void TR_04_TR_08_TR_10_Close_records_reason_and_time(ClaimStatus from)
    {
        var claim = ClaimIn(from);
        var justification = claim.OpenReserveTotal > 0 ? "Reserve kept for the invoice still to come." : null;

        claim.ChangeStatus(ClaimStatus.Closed, "Settled in full", justification, Handler, Transitions, Now);

        claim.Status.ShouldBe(ClaimStatus.Closed);
        claim.ClosedAt.ShouldBe(Now);
        claim.ClosureReason.ShouldBe("Settled in full");
        claim.SingleEvent<ClaimStatusChanged>().Reason.ShouldBe("Settled in full");
        claim.SingleEvent<ClaimClosed>().ClosureReason.ShouldBe("Settled in full");
    }

    [Fact]
    public void TR_04_Close_without_reason_is_rejected()
    {
        ShouldViolate(() => OpenClaim().ChangeStatus(ClaimStatus.Closed, "  ", null, Handler, Transitions, Now))[ErrorKeys.Reason]
            .ShouldBe(["A reason is required to move a claim to Closed."]);
    }

    [Theory]
    [InlineData(ClaimStatus.Open)]
    [InlineData(ClaimStatus.UnderInvestigation)]
    public void TR_05_TR_09_Withdrawal_requires_a_reason(ClaimStatus from)
    {
        var claim = ClaimIn(from);

        ShouldViolate(() => claim.ChangeStatus(ClaimStatus.Withdrawn, null, null, Handler, Transitions, Now))
            .ShouldContainKey(ErrorKeys.Reason);

        claim.ChangeStatus(ClaimStatus.Withdrawn, "Claimant withdrew the claim", null, Handler, Transitions, Now);

        claim.Status.ShouldBe(ClaimStatus.Withdrawn);
        claim.ClosureReason.ShouldBe("Claimant withdrew the claim");
        claim.ClosedAt.ShouldBe(Now);
    }

    [Fact]
    public void TR_05_Withdrawal_is_blocked_while_a_reserve_is_pending()
    {
        var claim = OpenClaim();
        Submit(claim, ReserveComponentType.Indemnity, 20_000m);

        ShouldViolate(() => claim.ChangeStatus(ClaimStatus.Withdrawn, "Withdrawn", null, Handler, Transitions, Now))[ErrorKeys.Reserves]
            .ShouldBe(["Claim cannot be withdrawn while a reserve transaction is pending approval."]);
    }

    [Fact]
    public void BR_ST_04_Reopen_moves_claim_to_open()
    {
        var claim = ClaimIn(ClaimStatus.Closed);

        claim.ChangeStatus(ClaimStatus.Reopened, "New medical evidence", null, Supervisor, Transitions, Now);

        claim.Status.ShouldBe(ClaimStatus.Open);
        claim.ClosedAt.ShouldBeNull();
        claim.ClosureReason.ShouldBeNull();

        // Two STATUS_CHANGED around one CLAIM_REOPENED (D-26), in this order.
        claim.DomainEvents.ShouldBe(
        [
            new ClaimStatusChanged(claim.Id, ClaimStatus.Closed, ClaimStatus.Reopened, "New medical evidence"),
            new ClaimReopened(claim.Id, "New medical evidence"),
            new ClaimStatusChanged(claim.Id, ClaimStatus.Reopened, ClaimStatus.Open, null),
        ]);
    }

    [Fact]
    public void BR_ST_04_Reopen_without_reason_is_rejected()
    {
        var claim = ClaimIn(ClaimStatus.Closed);

        ShouldViolate(() => claim.ChangeStatus(ClaimStatus.Reopened, " ", null, Supervisor, Transitions, Now))[ErrorKeys.Reason]
            .ShouldBe(["A reason is required to move a claim to Reopened."]);
        claim.Status.ShouldBe(ClaimStatus.Closed);
    }

    [Fact]
    public void BR_ST_04_Handler_cannot_reopen()
    {
        var claim = ClaimIn(ClaimStatus.Closed);

        Should.Throw<ForbiddenAccessException>(() => claim.ChangeStatus(ClaimStatus.Reopened, "Reason", null, Handler, Transitions, Now));
        claim.Status.ShouldBe(ClaimStatus.Closed);
    }

    [Fact]
    public void BR_ST_04_Manager_can_reopen_because_roles_are_hierarchical()
    {
        var claim = ClaimIn(ClaimStatus.Closed);

        claim.ChangeStatus(ClaimStatus.Reopened, "Reason", null, Manager, Transitions, Now);

        claim.Status.ShouldBe(ClaimStatus.Open);
    }

    [Fact]
    public void TR_12_Reopened_to_open_is_system_only()
    {
        Transitions.Find(ClaimStatus.Reopened, ClaimStatus.Open).ShouldNotBeNull().IsSystemOnly.ShouldBeTrue();
        Transitions.ValidNextStatuses(ClaimStatus.Reopened).ShouldBeEmpty();
    }

    [Fact]
    public void TR_12_Reopen_fails_safely_when_the_automatic_row_is_missing()
    {
        var withoutAutomaticRow = new StatusTransitionTable(ClaimStatusTransition.FrsDefaults().Where(row => !row.IsSystemOnly));
        var claim = ClaimIn(ClaimStatus.Closed);

        Should.Throw<InvalidOperationException>(() => claim.ChangeStatus(ClaimStatus.Reopened, "Reason", null, Supervisor, withoutAutomaticRow, Now));
    }

    public static TheoryData<string> WriteActions() =>
    [
        nameof(Claim.AddParty),
        nameof(Claim.RemoveParty),
        nameof(Claim.AddRiskObject),
        nameof(Claim.SubmitReserveTransaction),
        nameof(Claim.LinkPolicy),
        nameof(Claim.UpdateNotes),
        nameof(Claim.ChangeSeverity),
        nameof(Claim.AddDocument),
        nameof(Claim.SetReserveLimitOverride),
    ];

    [Theory]
    [MemberData(nameof(WriteActions))]
    public void TR_13_Changes_on_closed_and_withdrawn_claims_are_rejected(string action)
    {
        foreach (var status in new[] { ClaimStatus.Closed, ClaimStatus.Withdrawn })
        {
            var claim = ClaimIn(status);
            Action write = action switch
            {
                nameof(Claim.AddParty) => () => claim.AddParty(Witness(), Handler, Now),
                nameof(Claim.RemoveParty) => () => claim.RemoveParty(claim.Parties[0].Id, Handler, Now),
                nameof(Claim.AddRiskObject) => () => claim.AddRiskObject(Vehicle(), Handler, Now),
                nameof(Claim.SubmitReserveTransaction) => () => Submit(claim, ReserveComponentType.Indemnity, 100m),
                nameof(Claim.LinkPolicy) => () => claim.LinkPolicy(InForcePolicy(), Handler, Now),
                nameof(Claim.UpdateNotes) => () => claim.UpdateNotes("New note", Handler),
                nameof(Claim.ChangeSeverity) => () => claim.ChangeSeverity(ClaimSeverity.Critical, Handler),
                nameof(Claim.AddDocument) => () => claim.AddDocument(
                    DocumentBlobPath.For(Guid.NewGuid(), claim.Id, SequentialGuid.NewGuid(), SanitisedFileName.From("a.pdf")),
                    DocumentType.Invoice, 10, null, Handler, Now),
                nameof(Claim.SetReserveLimitOverride) => () => claim.SetReserveLimitOverride(true, "Reason", Manager, Now),
                _ => throw new ArgumentOutOfRangeException(nameof(action)),
            };

            ShouldViolate(write)[ErrorKeys.Claim].ShouldBe([$"Claim is {status}; no changes are permitted."]);
        }
    }
}
