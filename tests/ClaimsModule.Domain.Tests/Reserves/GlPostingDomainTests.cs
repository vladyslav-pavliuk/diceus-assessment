using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Reserves;
using static ClaimsModule.Domain.Tests.TestData;

namespace ClaimsModule.Domain.Tests.Reserves;

/// <summary>The domain side of GL posting: the simulated journal (FRS §6.5) and the retry rule (D-08).</summary>
public sealed class GlPostingDomainTests
{
    [Fact]
    public void JOB_04_Increase_debits_change_in_outstanding_reserves()
    {
        var entry = GlJournalEntry.ForReserveChange(25_000m);

        entry.ShouldBe(new GlJournalEntry("Change in Outstanding Reserves", "Outstanding Loss Reserves", 25_000m));
        entry.Lines.ShouldBe("DR Change in Outstanding Reserves / CR Outstanding Loss Reserves");
    }

    [Fact]
    public void JOB_04_Decrease_reverses_the_accounts_with_a_positive_amount()
    {
        GlJournalEntry.ForReserveChange(-8_000.5m)
            .ShouldBe(new GlJournalEntry("Outstanding Loss Reserves", "Change in Outstanding Reserves", 8_000.5m));
    }

    [Fact]
    public void JOB_04_A_zero_change_has_no_journal()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => GlJournalEntry.ForReserveChange(0m));
    }

    [Fact]
    public void API_25_A_posting_that_has_not_failed_cannot_be_retried()
    {
        var claim = OpenClaim();
        var autoApproved = Submit(claim, ReserveComponentType.Indemnity, 5_000m);
        var pending = Submit(claim, ReserveComponentType.Expense, 50_000m);

        ShouldViolate(() => claim.RetryGlPosting(autoApproved.Id, Handler))[ErrorKeys.GlPosting]
            .ShouldBe(["Only an approved transaction whose GL posting failed can be retried."]); // still Pending
        ShouldViolate(() => claim.RetryGlPosting(pending.Id, Handler))[ErrorKeys.GlPosting]
            .ShouldBe(["Only an approved transaction whose GL posting failed can be retried."]); // not approved
        autoApproved.PostingStatus.ShouldBe(PostingStatus.Pending);
    }

    [Fact]
    public void API_25_Unknown_transaction_is_not_found()
    {
        var claim = OpenClaim();

        Should.Throw<NotFoundException>(() => claim.RetryGlPosting(Guid.NewGuid(), Handler));
    }
}
