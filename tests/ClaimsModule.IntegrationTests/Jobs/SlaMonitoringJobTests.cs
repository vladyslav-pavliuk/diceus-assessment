using ClaimsModule.Application.Abstractions.ReadModels;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Infrastructure.BackgroundJobs;
using ClaimsModule.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.Jobs;

/// <summary>
/// SlaMonitoringJob (FRS §12.2, D-01) on the jobs host, whose clock the tests move. Assertions look only at the
/// claims a test created: other tests' claims in the same database are flagged too as the clock moves on.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SlaMonitoringJobTests(ApiFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan JustOver48Hours = TimeSpan.FromHours(48) + TimeSpan.FromSeconds(1);

    private JobsHost _host = null!;

    private TestDatabase Database => _host.Database;

    public async Task InitializeAsync() => _host = await fixture.JobsHostAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task JOB_08_Stale_draft_and_open_claims_are_flagged()
    {
        var draft = await NewClaimAsync();
        var open = await NewClaimAsync(ClaimStatus.Open);
        var underInvestigation = await NewClaimAsync(ClaimStatus.Open, ClaimStatus.UnderInvestigation);

        _host.Clock.Advance(JustOver48Hours);
        await RunSlaJobAsync();

        var entry = (await Breaches(draft)).ShouldHaveSingleItem();
        entry.Description.ShouldBe("Claim has not been updated in 48 hours"); // FRS §12.2 verbatim
        entry.CreatedByUserId.ShouldBeNull(); // the system actor (D-33)
        entry.CorrelationId.ShouldNotBeNull();
        (await Breaches(open)).ShouldHaveSingleItem();
        (await Breaches(underInvestigation)).ShouldBeEmpty(); // JOB_08: only Draft and Open are monitored
    }

    /// <summary>D-01: a claim never edited since creation has UpdatedAt = NULL, and it is exactly the stale kind.</summary>
    [Fact]
    public async Task JOB_08_Never_updated_claim_is_flagged()
    {
        var draft = await NewClaimAsync();
        (await Database.ClaimRowAsync(draft)).UpdatedAt.ShouldBeNull();

        _host.Clock.Advance(JustOver48Hours);
        await RunSlaJobAsync();

        (await Breaches(draft)).ShouldHaveSingleItem();
    }

    /// <summary>"UpdatedAt &lt; (now − 48 hours)": at exactly 48 hours the claim is not yet late.</summary>
    [Fact]
    public async Task JOB_08_Exactly_48_hours_is_not_yet_a_breach()
    {
        var open = await NewClaimAsync(ClaimStatus.Open);

        _host.Clock.Advance(TimeSpan.FromHours(48));
        await RunSlaJobAsync();
        (await Breaches(open)).ShouldBeEmpty();

        _host.Clock.Advance(TimeSpan.FromTicks(10)); // DATETIMEOFFSET(7) resolution is 100 ns
        await RunSlaJobAsync();
        (await Breaches(open)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task JOB_09_Second_run_within_24h_adds_nothing()
    {
        var draft = await NewClaimAsync();
        _host.Clock.Advance(JustOver48Hours);
        await RunSlaJobAsync();

        await RunSlaJobAsync(); // the next 15-minute run
        _host.Clock.Advance(TimeSpan.FromHours(24) - TimeSpan.FromSeconds(1));
        await RunSlaJobAsync();

        (await Breaches(draft)).ShouldHaveSingleItem();
    }

    /// <summary>"only add a new entry if 24+ hours have passed since the last SLA_BREACH_DETECTED entry".</summary>
    [Fact]
    public async Task JOB_09_Run_after_24h_adds_new_entry()
    {
        var draft = await NewClaimAsync();
        _host.Clock.Advance(JustOver48Hours);
        await RunSlaJobAsync();

        _host.Clock.Advance(TimeSpan.FromHours(24));
        await RunSlaJobAsync();

        var breaches = await Breaches(draft);
        breaches.Count.ShouldBe(2);
        (breaches[1].CreatedAt - breaches[0].CreatedAt).ShouldBe(TimeSpan.FromHours(24));
    }

    /// <summary>D-01: no status change (FRS §12.2), and no write at all, so the 48h clock and the RowVer stay put.</summary>
    [Fact]
    public async Task JOB_10_Sla_job_does_not_modify_claim_row()
    {
        var open = await NewClaimAsync(ClaimStatus.Open);
        var before = await Database.ClaimRowAsync(open);

        _host.Clock.Advance(JustOver48Hours);
        await RunSlaJobAsync();

        (await Breaches(open)).ShouldHaveSingleItem();
        var after = await Database.ClaimRowAsync(open);
        after.Status.ShouldBe(ClaimStatus.Open);
        after.UpdatedAt.ShouldBe(before.UpdatedAt);
        after.RowVer.ShouldBe(before.RowVer);
        (await DetailAsync(open)).IsSlaBreached.ShouldBeTrue(); // the computed flag (D-01)
    }

    /// <summary>Any change to the claim restarts the 48 hours and clears the computed flag.</summary>
    [Fact]
    public async Task JOB_08_Updating_a_breached_claim_restarts_the_clock()
    {
        var open = await NewClaimAsync(ClaimStatus.Open);
        _host.Clock.Advance(JustOver48Hours);
        await RunSlaJobAsync();

        await Database.ChangeStatusAsync(open, ClaimStatus.UnderInvestigation);
        await Database.ChangeStatusAsync(open, ClaimStatus.Open);
        (await DetailAsync(open)).IsSlaBreached.ShouldBeFalse();

        _host.Clock.Advance(TimeSpan.FromHours(47));
        await RunSlaJobAsync();
        (await Breaches(open)).Count.ShouldBe(1);

        _host.Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        await RunSlaJobAsync();
        (await Breaches(open)).Count.ShouldBe(2);
    }

    private async Task<Guid> NewClaimAsync(params ClaimStatus[] path)
    {
        var claim = await Database.CreateClaimAsync(_host.Clock.GetUtcNow().Year);
        foreach (var status in path)
        {
            await Database.ChangeStatusAsync(claim.Id, status);
        }

        return claim.Id;
    }

    private async Task RunSlaJobAsync()
    {
        await using var scope = _host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SlaMonitoringJob>().RunAsync(CancellationToken.None);
    }

    private Task<IReadOnlyList<ClaimAuditLog>> Breaches(Guid claimId) => Database.AuditAsync(claimId, AuditEventTypes.SlaBreachDetected);

    private async Task<Application.Claims.ClaimDetailDto> DetailAsync(Guid claimId)
    {
        await using var scope = await Database.TenantScopeAsync();
        return (await scope.ServiceProvider.GetRequiredService<IClaimQueries>().GetDetailAsync(claimId, 10, CancellationToken.None))!;
    }
}
