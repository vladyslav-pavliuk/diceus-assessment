using System.Text.Json;
using ClaimsModule.Application.Abstractions;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Claims.Commands.MarkGlPostingFailed;
using ClaimsModule.Application.Claims.Commands.PostGlReserveChange;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Infrastructure.BackgroundJobs;
using ClaimsModule.IntegrationTests.Fixtures;
using Hangfire;
using Hangfire.AspNetCore;
using Hangfire.Common;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.Jobs;

/// <summary>
/// PostGLReserveChangeJob (FRS §6.5, §12.1; BR-R-06; D-15, D-35, D-41; ARCHITECTURE-PLAN §6.1 R4–R10), run
/// directly against the jobs host's database, the way Hangfire runs it, but with the attempt chosen by the test.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class GlPostingJobTests(ApiFixture fixture) : IAsyncLifetime
{
    private JobsHost _host = null!;

    private TestDatabase Database => _host.Database;

    public async Task InitializeAsync()
    {
        _host = await fixture.JobsHostAsync();
        _host.Ledger.Reset();
    }

    public Task DisposeAsync()
    {
        _host.Ledger.Reset();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task BR_R_06_Job_run_twice_writes_one_gl_audit_entry()
    {
        var transaction = await AutoApprovedReserveAsync();

        (await RunJobAsync(transaction, new GlJobAttempt("job-1", 0))).ShouldBe(GlPostingOutcome.Posted);
        (await RunJobAsync(transaction, new GlJobAttempt("job-2", 0))).ShouldBe(GlPostingOutcome.NothingToPost);

        (await Database.AuditAsync(transaction.ClaimId, AuditEventTypes.GlPostingSimulated)).ShouldHaveSingleItem();
        _host.Ledger.Postings.Count(posting => posting.IdempotencyKey == transaction.IdempotencyKey).ShouldBe(1);
        (await Database.ReserveTransactionAsync(transaction.Id)).PostingJobId.ShouldBe("job-1"); // the run that posted
    }

    /// <summary>
    /// R6 with real overlap: run A has claimed the row and holds it (its ledger call is paused inside the
    /// transaction); run B is started and observed BLOCKED on A's lock in sys.dm_exec_requests; only then is A
    /// released. A check-then-act job would let B read "Pending" and post a second time.
    /// </summary>
    [Fact]
    public async Task BR_R_06_Job_run_concurrently_writes_one_gl_audit_entry()
    {
        var transaction = await AutoApprovedReserveAsync();

        var aIsInsideItsTransaction = _host.Ledger.HoldNextPosting();
        var runA = RunJobAsync(transaction, new GlJobAttempt("job-a", 0));
        await aIsInsideItsTransaction;

        var runB = RunJobAsync(transaction, new GlJobAttempt("job-b", 0));
        await SqlDiagnostics.WaitForBlockedRequestAsync(_host.Factory.ConnectionString, TimeSpan.FromSeconds(15));
        _host.Ledger.ReleaseHeldPosting();

        (await runA).ShouldBe(GlPostingOutcome.Posted);
        (await runB).ShouldBe(GlPostingOutcome.NothingToPost);
        (await Database.AuditAsync(transaction.ClaimId, AuditEventTypes.GlPostingSimulated)).ShouldHaveSingleItem();
        _host.Ledger.Postings.Count(posting => posting.IdempotencyKey == transaction.IdempotencyKey).ShouldBe(1);
    }

    /// <summary>R7: a failure after the status UPDATE rolls the UPDATE back too, so the retry starts clean.</summary>
    [Fact]
    public async Task JOB_03_Retry_after_failure_writes_single_entry()
    {
        var transaction = await AutoApprovedReserveAsync();
        _host.Ledger.FailNext(1);

        await Should.ThrowAsync<InvalidOperationException>(() => RunJobAsync(transaction, new GlJobAttempt("job-1", 0)));
        (await Database.ReserveTransactionAsync(transaction.Id)).PostingStatus.ShouldBe(PostingStatus.Pending);
        (await Database.AuditAsync(transaction.ClaimId, AuditEventTypes.GlPostingSimulated)).ShouldBeEmpty();

        (await RunJobAsync(transaction, new GlJobAttempt("job-1", 1))).ShouldBe(GlPostingOutcome.Posted);

        (await Database.AuditAsync(transaction.ClaimId, AuditEventTypes.GlPostingSimulated)).ShouldHaveSingleItem();
        (await Database.AuditAsync(transaction.ClaimId, AuditEventTypes.GlPostingFailed)).ShouldBeEmpty();
    }

    [Fact]
    public async Task JOB_04_Gl_audit_contains_journal_lines()
    {
        var transaction = await AutoApprovedReserveAsync(amount: 5_000m);

        await RunJobAsync(transaction, new GlJobAttempt("job-1", 0));

        var entry = (await Database.AuditAsync(transaction.ClaimId, AuditEventTypes.GlPostingSimulated)).Single();
        entry.Description.ShouldBe("GL posting simulated: DR Change in Outstanding Reserves / CR Outstanding Loss Reserves, Amount = $5,000.00.");
        entry.RelatedEntityId.ShouldBe(transaction.Id);
        entry.RelatedEntityType.ShouldBe("ReserveTransaction");

        var newValue = JsonDocument.Parse(entry.NewValue!).RootElement; // FRS §14.1: NewValue has the journal
        newValue.GetProperty("postingStatus").GetString().ShouldBe("Posted");
        newValue.GetProperty("journal").GetProperty("debitAccount").GetString().ShouldBe("Change in Outstanding Reserves");
        newValue.GetProperty("journal").GetProperty("creditAccount").GetString().ShouldBe("Outstanding Loss Reserves");
        newValue.GetProperty("journal").GetProperty("amount").GetDecimal().ShouldBe(5_000m);
        newValue.GetProperty("idempotencyKey").GetString().ShouldBe(transaction.IdempotencyKey);
        newValue.GetProperty("jobId").GetString().ShouldBe("job-1");
    }

    [Fact]
    public async Task JOB_04_A_decrease_reverses_the_journal_accounts()
    {
        var opened = await AutoApprovedReserveAsync(amount: 5_000m);
        await RunJobAsync(opened, new GlJobAttempt("job-1", 0));
        var decrease = await Database.SubmitReserveAsync(opened.ClaimId, ReserveComponentType.Indemnity, -2_000m);

        await RunJobAsync(decrease, new GlJobAttempt("job-2", 0));

        var entry = (await Database.AuditAsync(opened.ClaimId, AuditEventTypes.GlPostingSimulated)).Single(row => row.RelatedEntityId == decrease.Id);
        entry.Description.ShouldBe("GL posting simulated: DR Outstanding Loss Reserves / CR Change in Outstanding Reserves, Amount = $2,000.00.");
        _host.Ledger.Postings.Single(posting => posting.IdempotencyKey == decrease.IdempotencyKey).Entry
            .ShouldBe(new GlJournalEntry("Outstanding Loss Reserves", "Change in Outstanding Reserves", 2_000m));
    }

    /// <summary>R10: the job writes only its ReserveHistory row; the claim's RowVer and UpdatedAt do not move.</summary>
    [Fact]
    public async Task JOB_05_Success_sets_posted_and_job_id()
    {
        var transaction = await AutoApprovedReserveAsync();
        var claimBefore = await Database.ClaimRowAsync(transaction.ClaimId);

        await RunJobAsync(transaction, new GlJobAttempt("hangfire-42", 0));

        var posted = await Database.ReserveTransactionAsync(transaction.Id);
        posted.PostingStatus.ShouldBe(PostingStatus.Posted);
        posted.PostingJobId.ShouldBe("hangfire-42");

        var claimAfter = await Database.ClaimRowAsync(transaction.ClaimId);
        claimAfter.RowVer.ShouldBe(claimBefore.RowVer);
        claimAfter.UpdatedAt.ShouldBe(claimBefore.UpdatedAt);

        // JOB-12: the system actor, with the job's own correlation id (not the request's).
        var entry = (await Database.AuditAsync(transaction.ClaimId, AuditEventTypes.GlPostingSimulated)).Single();
        entry.CreatedByUserId.ShouldBeNull();
        entry.CorrelationId.ShouldNotBeNull();
        var submission = (await Database.AuditAsync(transaction.ClaimId, AuditEventTypes.ReserveCreated)).Single();
        entry.CorrelationId.ShouldNotBe(submission.CorrelationId);
    }

    [Fact]
    public async Task JOB_06_Final_attempt_failure_marks_failed_and_audits()
    {
        var transaction = await AutoApprovedReserveAsync();
        _host.Ledger.FailNext(int.MaxValue);

        // Not the last attempt: Hangfire will retry, so nothing is recorded yet.
        await Should.ThrowAsync<InvalidOperationException>(() => RunJobAsync(transaction, new GlJobAttempt("job-1", 2)));
        (await Database.ReserveTransactionAsync(transaction.Id)).PostingStatus.ShouldBe(PostingStatus.Pending);

        // The last attempt (3 retries done): Failed + GL_POSTING_FAILED, and the job still fails in Hangfire.
        await Should.ThrowAsync<InvalidOperationException>(
            () => RunJobAsync(transaction, new GlJobAttempt("job-1", PostGLReserveChangeJob.RetryAttempts)));

        var failed = await Database.ReserveTransactionAsync(transaction.Id);
        failed.PostingStatus.ShouldBe(PostingStatus.Failed);
        failed.PostingJobId.ShouldBe("job-1");
        var entry = (await Database.AuditAsync(transaction.ClaimId, AuditEventTypes.GlPostingFailed)).ShouldHaveSingleItem();
        entry.RelatedEntityId.ShouldBe(transaction.Id);
        var newValue = JsonDocument.Parse(entry.NewValue!).RootElement; // FRS §14.1: NewValue has the failure reason
        newValue.GetProperty("reason").GetString().ShouldBe("InvalidOperationException: Test ledger failure.");
        newValue.GetProperty("attempts").GetInt32().ShouldBe(4);
        (await Database.AuditAsync(transaction.ClaimId, AuditEventTypes.GlPostingSimulated)).ShouldBeEmpty();
    }

    /// <summary>R8: marking Failed is a compare-and-set too; it never overwrites a completed posting.</summary>
    [Fact]
    public async Task JOB_06_Failure_never_overwrites_a_posted_transaction()
    {
        var transaction = await AutoApprovedReserveAsync();
        await RunJobAsync(transaction, new GlJobAttempt("job-1", 0));

        var changed = await SendAsJobAsync(new MarkGlPostingFailedCommand(
            transaction.Id, transaction.ClaimId, transaction.IdempotencyKey, "job-2", 4, "Late failure of a duplicate run."));

        changed.ShouldBeFalse();
        (await Database.ReserveTransactionAsync(transaction.Id)).PostingStatus.ShouldBe(PostingStatus.Posted);
        (await Database.AuditAsync(transaction.ClaimId, AuditEventTypes.GlPostingFailed)).ShouldBeEmpty();
    }

    /// <summary>R9: a Failed posting is posted again only after the audited retry puts it back to Pending.</summary>
    [Fact]
    public async Task JOB_02_A_failed_posting_is_not_posted_by_a_stray_job()
    {
        var transaction = await FailedPostingAsync();

        (await RunJobAsync(transaction, new GlJobAttempt("stray", 0))).ShouldBe(GlPostingOutcome.NothingToPost);
        (await Database.ReserveTransactionAsync(transaction.Id)).PostingStatus.ShouldBe(PostingStatus.Failed);

        var handler = await Database.SeededUserAsync("handler.blake");
        await Database.ChangeClaimAsync(transaction.ClaimId, (claim, _) => Task.FromResult(claim.RetryGlPosting(transaction.Id, handler)));
        (await Database.ReserveTransactionAsync(transaction.Id)).PostingStatus.ShouldBe(PostingStatus.Pending);

        (await RunJobAsync(transaction, new GlJobAttempt("after-retry", 0))).ShouldBe(GlPostingOutcome.Posted);
        (await Database.AuditAsync(transaction.ClaimId)).Select(entry => entry.EventType).ShouldContain(AuditEventTypes.GlPostingRetried);
    }

    [Fact]
    public async Task JOB_02_Only_an_approved_transaction_matching_all_job_arguments_is_posted()
    {
        var claim = await Database.CreateClaimAsync(_host.Clock.GetUtcNow().Year);
        var pending = await Database.SubmitReserveAsync(claim.Id, ReserveComponentType.Indemnity, 50_000m);
        var approved = await AutoApprovedReserveAsync();

        (await RunJobAsync(pending, new GlJobAttempt("job-1", 0))).ShouldBe(GlPostingOutcome.NothingToPost); // not approved
        (await RunJobAsync(approved, new GlJobAttempt("job-2", 0), idempotencyKey: pending.IdempotencyKey))
            .ShouldBe(GlPostingOutcome.NothingToPost); // wrong key for this row

        (await Database.ReserveTransactionAsync(pending.Id)).PostingStatus.ShouldBe(PostingStatus.Pending);
        (await Database.ReserveTransactionAsync(approved.Id)).PostingStatus.ShouldBe(PostingStatus.Pending);
    }

    [Fact]
    public async Task JOB_12_Job_for_an_unknown_claim_does_nothing()
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<PostGLReserveChangeJob>();

        var outcome = await job.RunAsync(new GlPostingRequest(Guid.NewGuid(), Guid.NewGuid(), "Reserve:x:Change:1"), new GlJobAttempt(null, 0), CancellationToken.None);

        outcome.ShouldBeNull();
    }

    /// <summary>The database backstop: even a buggy writer cannot record a second posting of one transaction.</summary>
    [Fact]
    public async Task BR_R_06_Database_rejects_a_second_gl_posting_entry()
    {
        var transaction = await AutoApprovedReserveAsync();
        await RunJobAsync(transaction, new GlJobAttempt("job-1", 0));

        await using var scope = await Database.TenantScopeAsync();
        var services = scope.ServiceProvider;
        await Should.ThrowAsync<ConflictException>(() => services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
            _ =>
            {
                services.GetRequiredService<IAuditLogService>().Record(new AuditEntry(
                    transaction.ClaimId, AuditEventTypes.GlPostingSimulated, "Duplicate.", RelatedEntityId: transaction.Id));
                return Task.FromResult(true);
            },
            CancellationToken.None));
    }

    /// <summary>R4: an approved posting still Pending after 5 minutes is re-enqueued; a posted one is not.</summary>
    [Fact]
    public async Task JOB_11_Sweeper_enqueues_stranded_postings()
    {
        var transaction = await AutoApprovedReserveAsync();
        var enqueuedAfterCommit = HangfireJobs.EnqueuedGlPostings(_host.Services, transaction.Id).Count;
        enqueuedAfterCommit.ShouldBe(1);

        await RunSweeperAsync();
        HangfireJobs.EnqueuedGlPostings(_host.Services, transaction.Id).Count.ShouldBe(1); // within the grace period

        _host.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        await RunSweeperAsync();
        HangfireJobs.EnqueuedGlPostings(_host.Services, transaction.Id).Count.ShouldBe(2);

        await RunJobAsync(transaction, new GlJobAttempt("job-1", 0));
        _host.Clock.Advance(TimeSpan.FromMinutes(10));
        await RunSweeperAsync();
        HangfireJobs.EnqueuedGlPostings(_host.Services, transaction.Id).Count.ShouldBe(2);
    }

    /// <summary>
    /// JOB-01 end to end with a real Hangfire server: the after-commit enqueue, argument serialisation, activation
    /// from DI, PerformContext (job id) and the posting.
    /// </summary>
    [Fact]
    public async Task JOB_01_Hangfire_server_posts_an_approved_reserve_end_to_end()
    {
        var transaction = await AutoApprovedReserveAsync();
        var jobId = HangfireJobs.EnqueuedGlPostings(_host.Services, transaction.Id).Single().JobId;

        using (new BackgroundJobServer(
            new BackgroundJobServerOptions
            {
                ServerName = nameof(JOB_01_Hangfire_server_posts_an_approved_reserve_end_to_end),
                WorkerCount = 1,
                Activator = new AspNetCoreJobActivator(_host.Services.GetRequiredService<IServiceScopeFactory>()),
                FilterProvider = JobFilterProviders.Providers,
            },
            _host.Services.GetRequiredService<JobStorage>()))
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while ((await Database.ReserveTransactionAsync(transaction.Id)).PostingStatus != PostingStatus.Posted)
            {
                DateTime.UtcNow.ShouldBeLessThan(deadline, "The Hangfire server did not post the reserve change in time.");
                await Task.Delay(200);
            }
        }

        (await Database.ReserveTransactionAsync(transaction.Id)).PostingJobId.ShouldBe(jobId);
        (await Database.AuditAsync(transaction.ClaimId, AuditEventTypes.GlPostingSimulated)).ShouldHaveSingleItem();
    }

    private async Task<ReserveTransaction> AutoApprovedReserveAsync(decimal amount = 5_000m)
    {
        var claim = await Database.CreateClaimAsync(_host.Clock.GetUtcNow().Year);
        var transaction = await Database.SubmitReserveAsync(claim.Id, ReserveComponentType.Indemnity, amount);
        transaction.ApprovalStatus.ShouldBe(ReserveApprovalStatus.AutoApproved);
        return transaction;
    }

    private async Task<ReserveTransaction> FailedPostingAsync()
    {
        var transaction = await AutoApprovedReserveAsync();
        _host.Ledger.FailNext(1);
        await Should.ThrowAsync<InvalidOperationException>(
            () => RunJobAsync(transaction, new GlJobAttempt("job-1", PostGLReserveChangeJob.RetryAttempts)));
        (await Database.ReserveTransactionAsync(transaction.Id)).PostingStatus.ShouldBe(PostingStatus.Failed);
        return transaction;
    }

    private async Task<GlPostingOutcome?> RunJobAsync(ReserveTransaction transaction, GlJobAttempt attempt, string? idempotencyKey = null)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PostGLReserveChangeJob>().RunAsync(
            new GlPostingRequest(transaction.Id, transaction.ClaimId, idempotencyKey ?? transaction.IdempotencyKey), attempt, CancellationToken.None);
    }

    private async Task<TResult> SendAsJobAsync<TResult>(MediatR.IRequest<TResult> command)
    {
        return await _host.Services.GetRequiredService<JobScopes>().RunAsync(
            await Database.SeededOrganisationIdAsync(), Guid.NewGuid(), (sender, token) => sender.Send(command, token), CancellationToken.None);
    }

    private async Task RunSweeperAsync()
    {
        await using var scope = _host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<GlPostingSweeperJob>().RunAsync(CancellationToken.None);
    }
}
