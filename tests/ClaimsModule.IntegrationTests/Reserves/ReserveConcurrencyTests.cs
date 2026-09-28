using System.Net;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.IntegrationTests.Fixtures;

namespace ClaimsModule.IntegrationTests.Reserves;

/// <summary>
/// The races of ARCHITECTURE-PLAN §6.1 R1–R3, through HTTP. Two requests racing "at the same time" would usually
/// just run one after the other, and the second would get an ordinary 422, which proves nothing about
/// concurrency. The <see cref="ConcurrencyGate"/> forces the worst case instead: both requests have loaded the
/// claim and passed every domain check before either saves. Exactly one must commit; the other must get 409
/// and leave no trace (no audit row, no GL job).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ReserveConcurrencyTests(ApiFixture fixture) : IAsyncLifetime
{
    private ClaimsApi _handler = null!;
    private ClaimsApi _supervisor = null!;
    private ClaimsApi _otherSupervisor = null!;
    private ClaimsApi _manager = null!;
    private ClaimsApi _otherManager = null!;

    private ConcurrencyGate Gate => fixture.Factory.Gate;

    public async Task InitializeAsync()
    {
        _handler = await ClaimsApi.SignInAsync(fixture.Factory, "handler.alex");
        _supervisor = await ClaimsApi.SignInAsync(fixture.Factory, "supervisor.casey");
        _otherSupervisor = await ClaimsApi.SignInAsync(fixture.Factory, "supervisor.drew");
        _manager = await ClaimsApi.SignInAsync(fixture.Factory, "manager.emery");
        _otherManager = await ClaimsApi.SignInAsync(fixture.Factory, "manager.finley");
    }

    public Task DisposeAsync()
    {
        Gate.Disarm();
        return Task.CompletedTask;
    }

    /// <summary>R1: two approvers approve the same pending transaction at once.</summary>
    [Fact]
    public async Task RSV_07_Concurrent_approvals_one_wins()
    {
        var claim = await _handler.CreateClaimAsync();
        var transaction = (await _handler.SubmitReserveAsync(claim.Id, "Indemnity", 50_000m)).Transaction;

        Gate.Arm(participants: 2);
        var responses = await Task.WhenAll(
            _supervisor.DecideAsync(claim.Id, transaction.Id, "approve"),
            _otherSupervisor.DecideAsync(claim.Id, transaction.Id, "approve"));

        responses.Select(response => response.StatusCode).ShouldBe([HttpStatusCode.OK, HttpStatusCode.Conflict], ignoreOrder: true);
        await responses.Single(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBeProblemAsync(HttpStatusCode.Conflict, "Conflict");

        var audit = await _handler.AuditAsync(claim.Id);
        audit.Count(entry => entry.EventType == AuditEventTypes.ReserveApproved).ShouldBe(1);
        HangfireJobs.EnqueuedGlPostings(fixture.Factory.Services, transaction.Id).Count.ShouldBe(1); // the loser enqueued nothing (R5)
        (await _handler.GetReservesAsync(claim.Id)).Components.Single().CurrentAmount.ShouldBe(50_000m); // counted once
    }

    /// <summary>R2: two adjustments of the same component at once would fork the balance chain.</summary>
    [Fact]
    public async Task RSV_05_Concurrent_submissions_on_same_component_one_gets_409()
    {
        var claim = await _handler.CreateClaimAsync();
        await _handler.SubmitReserveAsync(claim.Id, "Indemnity", 5_000m);

        Gate.Arm(participants: 2);
        var responses = await Task.WhenAll(
            _handler.PostReserveAsync(claim.Id, new { component = "Indemnity", amount = 1_000m, changeReason = "Tow invoice." }),
            _supervisor.PostReserveAsync(claim.Id, new { component = "Indemnity", amount = 2_000m, changeReason = "Hire car." }));

        responses.Select(response => response.StatusCode).ShouldBe([HttpStatusCode.Created, HttpStatusCode.Conflict], ignoreOrder: true);

        var reserves = await _handler.GetReservesAsync(claim.Id);
        reserves.Transactions.Select(transaction => transaction.ChangeSequence).ShouldBe([2, 1]); // no duplicate sequence
        var winner = reserves.Transactions[0];
        winner.PreviousBalance.ShouldBe(5_000m);
        reserves.Components.Single().CurrentAmount.ShouldBe(winner.NewBalance); // one linear chain
        (await _handler.AuditAsync(claim.Id)).Count(entry => entry.EventType == AuditEventTypes.ReserveCreated).ShouldBe(2);
    }

    /// <summary>
    /// R3, write skew: two approvals on DIFFERENT components each pass the $10M check against their own snapshot
    /// (0 + 6M ≤ 10M) and update different component rows. Only the claim's RowVer, which every change to the
    /// aggregate touches, stops the claim ending at $12M.
    /// </summary>
    [Fact]
    public async Task BR_R_05_Concurrent_approvals_cannot_jointly_exceed_limit()
    {
        var claim = await _handler.CreateClaimAsync();
        var indemnity = (await _handler.SubmitReserveAsync(claim.Id, "Indemnity", 6_000_000m)).Transaction;
        var expense = (await _handler.SubmitReserveAsync(claim.Id, "Expense", 6_000_000m)).Transaction;
        indemnity.ExceedsAggregateLimit.ShouldBeFalse(); // nothing approved yet at submission
        expense.ExceedsAggregateLimit.ShouldBeFalse();

        Gate.Arm(participants: 2);
        var responses = await Task.WhenAll(
            _manager.DecideAsync(claim.Id, indemnity.Id, "approve"),
            _otherManager.DecideAsync(claim.Id, expense.Id, "approve"));

        responses.Select(response => response.StatusCode).ShouldBe([HttpStatusCode.OK, HttpStatusCode.Conflict], ignoreOrder: true);
        (await _handler.GetReservesAsync(claim.Id)).ApprovedAggregate.ShouldBe(6_000_000m);

        // The loser's retry re-reads the claim and now meets the limit (422), as a sequential approval would.
        var loser = responses[0].StatusCode == HttpStatusCode.Conflict ? (_manager, indemnity) : (_otherManager, expense);
        var retry = await loser.Item1.DecideAsync(claim.Id, loser.Item2.Id, "approve");
        (await ClaimsApi.ErrorsAsync(retry))["ReserveAmount"].ShouldBe(["Total reserves will exceed $10,000,000. Manager override required."]);
    }

    /// <summary>R1 variant: the submitter retracts while a supervisor approves.</summary>
    [Fact]
    public async Task RSV_07_Retract_racing_an_approval_leaves_one_outcome()
    {
        var claim = await _handler.CreateClaimAsync();
        var transaction = (await _handler.SubmitReserveAsync(claim.Id, "Indemnity", 40_000m)).Transaction;

        // Retract raises no gated event, so only the approval waits at the gate. Order forced: the approval has
        // loaded the claim, then the retraction commits, then the approval tries to save its stale copy.
        Gate.Arm(participants: 2);
        var approval = _supervisor.DecideAsync(claim.Id, transaction.Id, "approve");
        await Gate.FirstArrival;
        var retraction = await _handler.DecideAsync(claim.Id, transaction.Id, "retract");
        Gate.Disarm();

        retraction.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await approval).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await _handler.GetReservesAsync(claim.Id)).Transactions.Single().ApprovalStatus.ShouldBe(ReserveApprovalStatus.Cancelled);
        HangfireJobs.EnqueuedGlPostings(fixture.Factory.Services, transaction.Id).ShouldBeEmpty();
    }
}
