using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Claims.Events;
using static ClaimsModule.Domain.Tests.TestData;

namespace ClaimsModule.Domain.Tests.Claims;

public sealed class ClaimCreationTests
{
    [Fact]
    public void FRS_5_2_New_claim_is_a_draft_assigned_to_its_creator()
    {
        var claim = Claim.Create(ClaimNumber.Create(2026, 142), InForcePolicy(), Loss(), ClaimSeverity.Minor, [Claimant()], [Vehicle()], Supervisor, Now);

        claim.Status.ShouldBe(ClaimStatus.Draft);
        claim.ClaimNumber.ShouldBe("CLM-2026-0000142");
        claim.AssignedHandlerId.ShouldBe(Supervisor.UserId); // D-18
        claim.ReportedDate.ShouldBe(Now);
        claim.LossEvent.ReportDate.ShouldBe(Now);
        claim.PolicyNumber.ShouldBe("POL-TEST-000001");
        claim.ClientName.ShouldBe("Test Client Ltd");
        claim.SingleEvent<ClaimCreated>().ClaimNumber.ShouldBe("CLM-2026-0000142");
        claim.DomainEvents.OfType<PartyAdded>().ShouldHaveSingleItem();
        claim.DomainEvents.OfType<RiskObjectAdded>().ShouldHaveSingleItem();
        claim.ValidationIssues.ShouldBeEmpty();
    }

    [Fact]
    public void BR_C_01_Future_loss_date_is_rejected()
    {
        var errors = ShouldViolate(() => DraftClaim(loss: Loss(Now.AddSeconds(1))));

        errors[ErrorKeys.LossDate].ShouldBe([DomainMessages.LossDateInFuture]);
        DomainMessages.LossDateInFuture.ShouldBe("Loss date cannot be in the future."); // VAL-01, FRS §8 verbatim
    }

    [Fact]
    public void BR_C_01_Loss_date_equal_to_now_is_accepted()
    {
        DraftClaim(loss: Loss(Now)).LossEvent.LossDate.ShouldBe(Now);
    }

    [Fact]
    public void BR_C_01_Loss_date_is_stored_in_utc()
    {
        var local = new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.FromHours(3));

        var stored = DraftClaim(loss: Loss(local)).LossEvent.LossDate;

        stored.Offset.ShouldBe(TimeSpan.Zero);
        stored.ShouldBe(local);
    }

    [Fact]
    public void VAL_02_Missing_loss_date_is_rejected()
    {
        ShouldViolate(() => DraftClaim(loss: Loss(default(DateTimeOffset))))[ErrorKeys.LossDate]
            .ShouldBe(["Loss date is required."]);
    }

    [Theory]
    [InlineData("Too short: 19 chars")]
    [InlineData("   Too short: 19 chars   ")]
    [InlineData("")]
    public void BR_C_07_Description_shorter_than_20_chars_is_rejected(string description)
    {
        ShouldViolate(() => DraftClaim(loss: Loss(description: description)))[ErrorKeys.LossDescription]
            .ShouldBe(["Loss description is required and must be at least 20 characters."]);
    }

    [Fact]
    public void BR_C_07_Description_of_exactly_20_chars_is_accepted()
    {
        DraftClaim(loss: Loss(description: "Exactly twenty chars")).LossEvent.LossDescription.Length.ShouldBe(20);
    }

    [Fact]
    public void BR_C_05_Blank_cause_of_loss_code_is_rejected()
    {
        var loss = Loss() with { CauseOfLossCode = " " };

        ShouldViolate(() => DraftClaim(loss: loss))[ErrorKeys.CauseOfLossCode]
            .ShouldBe(["Cause of loss code is not recognised or is inactive."]);
    }

    [Fact]
    public void FRS_10_4_All_loss_event_errors_are_reported_together()
    {
        var loss = Loss(Now.AddDays(1), "short") with { EstimatedLossAmount = -1m };

        var errors = ShouldViolate(() => DraftClaim(loss: loss));

        errors.Keys.ShouldBe([ErrorKeys.LossDate, ErrorKeys.LossDescription, ErrorKeys.EstimatedLossAmount], ignoreOrder: true);
    }

    [Fact]
    public void BR_C_02_Loss_date_outside_policy_period_raises_warning()
    {
        var expired = PolicyCovering(new DateOnly(2020, 1, 1), new DateOnly(2021, 12, 31));

        var claim = DraftClaim(policy: expired);

        var issue = claim.ValidationIssues.ShouldHaveSingleItem();
        issue.RuleCode.ShouldBe(ValidationRuleCodes.LossDateOutsidePolicyPeriod);
        issue.Severity.ShouldBe(IssueSeverity.Warning);
        issue.Field.ShouldBe(ErrorKeys.LossDate);
        issue.Message.ShouldBe("Loss date is outside the policy effective period."); // VAL-03
        issue.Status.ShouldBe(IssueStatus.Open);
        claim.Status.ShouldBe(ClaimStatus.Draft); // "The claim can be created (it remains in Draft)"
    }

    [Theory]
    [InlineData("2026-09-01", false)] // effective date
    [InlineData("2026-09-25", false)] // expiration date
    [InlineData("2026-08-31", true)]  // day before
    [InlineData("2026-09-26", true)]  // day after
    public void BR_C_02_Boundary_dates_are_inclusive(string lossDay, bool expectWarning)
    {
        var policy = PolicyCovering(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 25));
        var lossDate = new DateTimeOffset(DateTime.Parse(lossDay, System.Globalization.CultureInfo.InvariantCulture), TimeSpan.Zero).AddHours(12);

        var claim = DraftClaim(policy: policy, loss: Loss(lossDate));

        claim.ValidationIssues.Any(issue => issue.RuleCode == ValidationRuleCodes.LossDateOutsidePolicyPeriod).ShouldBe(expectWarning);
    }

    [Fact]
    public void BR_C_02_The_utc_calendar_date_of_the_loss_is_compared()
    {
        // 2026-09-25 22:00 at UTC-05:00 is 2026-09-26 03:00 UTC: after a policy that expires on the 25th (D-32).
        var policy = PolicyCovering(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 25));
        var lossDate = new DateTimeOffset(2026, 9, 25, 22, 0, 0, TimeSpan.FromHours(-5));

        DraftClaim(policy: policy, loss: Loss(lossDate)).ValidationIssues
            .ShouldContain(issue => issue.RuleCode == ValidationRuleCodes.LossDateOutsidePolicyPeriod);
    }

    [Fact]
    public void BR_C_03_Claim_without_claimant_is_created_with_critical_issue()
    {
        var claim = DraftClaim(withClaimant: false);

        var issue = claim.ValidationIssues.ShouldHaveSingleItem();
        issue.RuleCode.ShouldBe(ValidationRuleCodes.NoClaimant);
        issue.Severity.ShouldBe(IssueSeverity.Critical);
        issue.Field.ShouldBe(ErrorKeys.ClaimParties);
        issue.Message.ShouldBe("At least one Claimant party is required to open a claim."); // VAL-07
        claim.Status.ShouldBe(ClaimStatus.Draft);
    }

    [Fact]
    public void BR_C_03_Adding_claimant_resolves_issue()
    {
        var claim = DraftClaim(withClaimant: false);

        claim.AddParty(Claimant(), Handler, Now);

        var issue = claim.ValidationIssues.ShouldHaveSingleItem();
        issue.Status.ShouldBe(IssueStatus.Resolved);
        issue.ResolvedAt.ShouldBe(Now);
        claim.SingleEvent<ValidationIssueResolved>().RuleCode.ShouldBe(ValidationRuleCodes.NoClaimant);
    }

    [Fact]
    public void BR_C_03_Adding_a_non_claimant_does_not_resolve_issue()
    {
        var claim = DraftClaim(withClaimant: false);

        claim.AddParty(Witness(), Handler, Now);

        claim.ValidationIssues.ShouldHaveSingleItem().Status.ShouldBe(IssueStatus.Open);
    }

    [Fact]
    public void BR_C_06_Claim_without_policy_gets_warning()
    {
        var claim = DraftClaim(withPolicy: false);

        var issue = claim.ValidationIssues.ShouldHaveSingleItem();
        issue.RuleCode.ShouldBe(ValidationRuleCodes.NoPolicy);
        issue.Severity.ShouldBe(IssueSeverity.Warning);
        issue.Message.ShouldBe("No policy linked. Policy must be associated before reserves can be set."); // VAL-06
        claim.PolicyId.ShouldBeNull();
        claim.PolicyNumber.ShouldBeNull();
        claim.ClientName.ShouldBeNull();
    }

    [Fact]
    public void BR_C_06_Linking_policy_resolves_warning_and_unblocks_reserves()
    {
        var claim = DraftClaim(withPolicy: false);
        var policy = InForcePolicy();

        claim.LinkPolicy(policy, Handler, Now);

        claim.PolicyId.ShouldBe(policy.Id);
        claim.PolicyNumber.ShouldBe(policy.PolicyNumber);
        claim.ValidationIssues.ShouldHaveSingleItem().Status.ShouldBe(IssueStatus.Resolved);
        claim.SingleEvent<PolicyLinked>().PreviousPolicyId.ShouldBeNull();
        Submit(claim, ClaimsModule.Domain.Reserves.ReserveComponentType.Indemnity, 1_000m).ShouldNotBeNull();
    }

    [Fact]
    public void BR_C_06_Linking_an_expired_policy_raises_the_period_warning()
    {
        var claim = DraftClaim(withPolicy: false);

        claim.LinkPolicy(PolicyCovering(new DateOnly(2020, 1, 1), new DateOnly(2021, 12, 31)), Handler, Now);

        claim.ValidationIssues.ShouldContain(issue =>
            issue.RuleCode == ValidationRuleCodes.LossDateOutsidePolicyPeriod && issue.Status == IssueStatus.Open);
    }

    [Fact]
    public void FRS_5_4_Claim_without_risk_objects_gets_warning()
    {
        var issue = DraftClaim(withRiskObject: false).ValidationIssues.ShouldHaveSingleItem();

        issue.RuleCode.ShouldBe(ValidationRuleCodes.NoRiskObject);
        issue.Severity.ShouldBe(IssueSeverity.Warning);
    }

    [Fact]
    public void D_07_Each_new_issue_raises_an_event()
    {
        var claim = Claim.Create(ClaimNumber.Create(2026, 1), null, Loss(), ClaimSeverity.Standard, [], [], Handler, Now);

        claim.DomainEvents.OfType<ValidationIssueRaised>().Select(raised => raised.RuleCode)
            .ShouldBe([ValidationRuleCodes.NoClaimant, ValidationRuleCodes.NoPolicy, ValidationRuleCodes.NoRiskObject], ignoreOrder: true);
    }

    [Fact]
    public void BR_P_01_Claim_can_exist_with_zero_parties()
    {
        DraftClaim(withClaimant: false).Parties.ShouldBeEmpty();
    }

    [Fact]
    public void BR_P_02_Multiple_claimants_are_allowed()
    {
        var claim = DraftClaim();

        claim.AddParty(Claimant("Anna"), Handler, Now);

        claim.Parties.Count(party => party.IsActiveClaimant).ShouldBe(2);
    }

    [Theory]
    [InlineData(PartyRole.Claimant)]
    [InlineData(PartyRole.Insured)]
    [InlineData(PartyRole.ThirdParty)]
    [InlineData(PartyRole.Witness)]
    [InlineData(PartyRole.Attorney)]
    public void BR_P_02_Every_party_role_is_supported(PartyRole role)
    {
        var claim = DraftClaim();

        var party = claim.AddParty(Claimant() with { Role = role }, Handler, Now);

        party.PartyRole.ShouldBe(role);
        party.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void BR_P_02_Unknown_party_role_is_rejected()
    {
        ShouldViolate(() => DraftClaim().AddParty(Claimant() with { Role = (PartyRole)99 }, Handler, Now))
            .ShouldContainKey(ErrorKeys.ClaimParties);
    }

    [Fact]
    public void FRS_9_3_A_person_needs_first_and_last_name()
    {
        ShouldViolate(() => DraftClaim().AddParty(Claimant() with { LastName = " " }, Handler, Now))[ErrorKeys.ClaimParties]
            .ShouldBe(["First name and last name are required for a person."]);
    }

    [Fact]
    public void FRS_9_3_A_company_needs_a_company_name_and_keeps_no_person_names()
    {
        var company = new PartyDetails(PartyRole.ThirdParty, PartyType.Company, "Ignored", "Ignored", "Acme Freight Ltd", null, null, null);

        var party = DraftClaim().AddParty(company, Handler, Now);

        party.CompanyName.ShouldBe("Acme Freight Ltd");
        party.FirstName.ShouldBeNull();
        ShouldViolate(() => DraftClaim().AddParty(company with { CompanyName = null }, Handler, Now))[ErrorKeys.ClaimParties]
            .ShouldBe(["Company name is required for a company."]);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    [InlineData("a b@example.com")]
    public void FRS_9_3_Malformed_email_is_rejected(string email)
    {
        ShouldViolate(() => DraftClaim().AddParty(Claimant() with { Email = email }, Handler, Now))
            .ShouldContainKey(ErrorKeys.ClaimParties);
    }

    [Fact]
    public void FRS_9_4_First_risk_object_is_primary_and_a_new_primary_replaces_it()
    {
        var claim = DraftClaim(withRiskObject: false);

        var first = claim.AddRiskObject(Vehicle(), Handler, Now);
        var second = claim.AddRiskObject(Vehicle(), Handler, Now);
        first.IsPrimary.ShouldBeTrue();
        second.IsPrimary.ShouldBeFalse();

        var third = claim.AddRiskObject(Vehicle(isPrimary: true), Handler, Now);

        claim.RiskObjects.Single(riskObject => riskObject.IsPrimary).ShouldBe(third);
    }
}
