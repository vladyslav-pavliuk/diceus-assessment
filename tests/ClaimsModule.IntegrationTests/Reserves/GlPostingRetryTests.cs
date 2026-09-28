using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Claims;
using ClaimsModule.Domain.Audit;
using ClaimsModule.Domain.Reserves;
using ClaimsModule.Infrastructure.BackgroundJobs;
using ClaimsModule.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.Reserves;

/// <summary>
/// POST /claims/{id}/reserves/{txnId}/retry-posting (FRS §11.3 "retry button for Failed", D-08): the way back from a
/// failed GL posting. The failure itself is produced by the real job with a ledger that always fails.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class GlPostingRetryTests(ApiFixture fixture) : IAsyncLifetime
{
    private ClaimsApi _handler = null!;

    public async Task InitializeAsync()
    {
        _handler = await ClaimsApi.SignInAsync(fixture.Factory, "handler.alex");
        fixture.Factory.Ledger.Reset();
    }

    public Task DisposeAsync()
    {
        fixture.Factory.Ledger.Reset();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task API_25_Retry_failed_posting()
    {
        var claim = await _handler.CreateClaimAsync();
        var transaction = (await _handler.SubmitReserveAsync(claim.Id, "Indemnity", 7_500m)).Transaction;
        fixture.Factory.Ledger.FailNext(int.MaxValue);
        await Should.ThrowAsync<InvalidOperationException>(() => RunJobAsync(transaction, PostGLReserveChangeJob.RetryAttempts));
        fixture.Factory.Ledger.Reset();

        var response = await _handler.Client.PostAsync($"/api/claims/{claim.Id}/reserves/{transaction.Id}/retry-posting", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await response.Content.ReadFromJsonAsync<ReserveTransactionDto>(TestAuth.Json))!.PostingStatus.ShouldBe(PostingStatus.Pending);
        var audit = await _handler.AuditAsync(claim.Id);
        audit[0].EventType.ShouldBe(AuditEventTypes.GlPostingRetried); // AUD-17
        audit[1].EventType.ShouldBe(AuditEventTypes.GlPostingFailed); // AUD-11
        HangfireJobs.EnqueuedGlPostings(fixture.Factory.Services, transaction.Id).Count.ShouldBe(2); // approval + retry

        await RunJobAsync(transaction, retryCount: 0);
        var row = (await _handler.GetReservesAsync(claim.Id)).Transactions.Single();
        row.PostingStatus.ShouldBe(PostingStatus.Posted);
        (await _handler.AuditAsync(claim.Id))[0].EventType.ShouldBe(AuditEventTypes.GlPostingSimulated);
    }

    [Fact]
    public async Task API_25_Only_a_failed_posting_can_be_retried()
    {
        var claim = await _handler.CreateClaimAsync();
        var transaction = (await _handler.SubmitReserveAsync(claim.Id, "Indemnity", 7_500m)).Transaction;

        var response = await _handler.Client.PostAsync($"/api/claims/{claim.Id}/reserves/{transaction.Id}/retry-posting", null);

        (await ClaimsApi.ErrorsAsync(response))["GlPosting"].ShouldBe(["Only an approved transaction whose GL posting failed can be retried."]);
        (await _handler.Client.PostAsync($"/api/claims/{claim.Id}/reserves/{Guid.NewGuid()}/retry-posting", null))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task RunJobAsync(ReserveTransactionDto transaction, int retryCount)
    {
        var claimId = (await new TestDatabase(fixture).ReserveTransactionAsync(transaction.Id)).ClaimId;
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PostGLReserveChangeJob>().RunAsync(
            new GlPostingRequest(transaction.Id, claimId, transaction.IdempotencyKey), new GlJobAttempt("job-1", retryCount), CancellationToken.None);
    }
}
