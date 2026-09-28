using System.ComponentModel;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Claims.Commands.MarkGlPostingFailed;
using ClaimsModule.Application.Claims.Commands.PostGlReserveChange;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// Thin entry point: sets the claim's tenant (D-31) and sends <see cref="PostGlReserveChangeCommand"/>. After the last retry
/// it marks the posting Failed in a new unit of work and rethrows, so the dashboard shows the failure too (D-35, D-41).
/// </summary>
public sealed class PostGLReserveChangeJob(ITenantDirectory tenants, JobScopes jobScopes, ILogger<PostGLReserveChangeJob> logger)
{
    /// <summary>A constant, because attribute arguments must be.</summary>
    public const int RetryAttempts = 3;

    /// <summary>Hangfire supplies <paramref name="context"/> and the cancellation token at run time.</summary>
    [AutomaticRetry(Attempts = RetryAttempts, DelaysInSeconds = [10, 30, 60], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [DisplayName("GL posting {2}")]
    public Task ExecuteAsync(Guid reserveHistoryId, Guid claimId, string idempotencyKey, PerformContext? context, CancellationToken cancellationToken) =>
        RunAsync(new GlPostingRequest(reserveHistoryId, claimId, idempotencyKey), GlJobAttempt.From(context), cancellationToken);

    /// <summary>Public so tests can drive a given attempt without a Hangfire server.</summary>
    public async Task<GlPostingOutcome?> RunAsync(GlPostingRequest request, GlJobAttempt attempt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(attempt);

        var organisationId = await tenants.FindOrganisationOfClaimAsync(request.ClaimId, cancellationToken);
        if (organisationId is null)
        {
            logger.LogWarning("GL posting {IdempotencyKey}: claim {ClaimId} does not exist; nothing to post", request.IdempotencyKey, request.ClaimId);
            return null;
        }

        var correlationId = Guid.NewGuid();
        try
        {
            return await jobScopes.RunAsync(
                organisationId.Value,
                correlationId,
                (sender, token) => sender.Send(
                    new PostGlReserveChangeCommand(request.ReserveHistoryId, request.ClaimId, request.IdempotencyKey, attempt.JobId), token),
                cancellationToken);
        }
        catch (Exception exception) when (attempt.IsFinal && exception is not OperationCanceledException)
        {
            // The posting's transaction has rolled back, so the row is still Pending; record the failure in a new one.
            logger.LogError(exception, "GL posting {IdempotencyKey} failed on attempt {Attempt}, the last one", request.IdempotencyKey, attempt.Number);

            await jobScopes.RunAsync(
                organisationId.Value,
                correlationId,
                (sender, token) => sender.Send(
                    new MarkGlPostingFailedCommand(
                        request.ReserveHistoryId,
                        request.ClaimId,
                        request.IdempotencyKey,
                        attempt.JobId,
                        attempt.Number,
                        $"{exception.GetType().Name}: {exception.Message}"),
                    token),
                cancellationToken);

            throw;
        }
    }
}

public sealed record GlJobAttempt(string? JobId, int RetryCount)
{
    /// <summary>Written by Hangfire's AutomaticRetry filter.</summary>
    private const string RetryCountParameter = "RetryCount";

    public int Number => RetryCount + 1;

    public bool IsFinal => RetryCount >= PostGLReserveChangeJob.RetryAttempts;

    public static GlJobAttempt From(PerformContext? context) =>
        new(context?.BackgroundJob.Id, context?.GetJobParameter<int>(RetryCountParameter) ?? 0);
}
