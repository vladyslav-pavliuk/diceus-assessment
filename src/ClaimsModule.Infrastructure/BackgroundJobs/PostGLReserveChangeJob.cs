using System.ComponentModel;
using ClaimsModule.Application.Abstractions.Persistence;
using ClaimsModule.Application.Claims.Commands.MarkGlPostingFailed;
using ClaimsModule.Application.Claims.Commands.PostGlReserveChange;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// PostGLReserveChangeJob (FRS §12.1, Brief §3.5): enqueued after an approval commits (auto or manual), after a
/// retry of a failed posting, and by the sweeper (D-15). It is a thin Hangfire entry point: it finds the claim's
/// organisation (D-31) and sends <see cref="PostGlReserveChangeCommand"/>, which does the idempotent work in one
/// transaction (ARCHITECTURE-PLAN §6.1 R6, R7).
/// <para>
/// Retries (D-35, amended by D-41): Hangfire retries a failed run <see cref="RetryAttempts"/> times, after
/// 10, 30 and 60 seconds, so four runs in all. When the last run fails, the job marks the posting Failed and
/// writes GL_POSTING_FAILED in a new unit of work, then rethrows, so the Hangfire dashboard shows the job as
/// Failed too. The way back is the audited retry endpoint, not a requeue in the dashboard (which is read-only).
/// </para>
/// </summary>
public sealed class PostGLReserveChangeJob(ITenantDirectory tenants, JobScopes jobScopes, ILogger<PostGLReserveChangeJob> logger)
{
    /// <summary>Retries after the first run. A constant, because attribute arguments must be.</summary>
    public const int RetryAttempts = 3;

    /// <summary>
    /// The method Hangfire invokes. Hangfire passes the <paramref name="context"/> and the shutdown token itself;
    /// the enqueue passes null and <see cref="CancellationToken.None"/> for them.
    /// </summary>
    [AutomaticRetry(Attempts = RetryAttempts, DelaysInSeconds = [10, 30, 60], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [DisplayName("GL posting {2}")]
    public Task ExecuteAsync(Guid reserveHistoryId, Guid claimId, string idempotencyKey, PerformContext? context, CancellationToken cancellationToken) =>
        RunAsync(new GlPostingRequest(reserveHistoryId, claimId, idempotencyKey), GlJobAttempt.From(context), cancellationToken);

    /// <summary>One run of the job. Public so that tests can drive a given attempt without a Hangfire server.</summary>
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

        // One correlation id per run, shared by the posting attempt and, if it fails, the failure record.
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

/// <summary>Which run of the GL job this is: the Hangfire job id and how many retries came before it.</summary>
public sealed record GlJobAttempt(string? JobId, int RetryCount)
{
    /// <summary>Hangfire's AutomaticRetry filter stores the number of retries so far under this job parameter.</summary>
    private const string RetryCountParameter = "RetryCount";

    public int Number => RetryCount + 1;

    /// <summary>After this run fails, Hangfire schedules no more retries.</summary>
    public bool IsFinal => RetryCount >= PostGLReserveChangeJob.RetryAttempts;

    public static GlJobAttempt From(PerformContext? context) =>
        new(context?.BackgroundJob.Id, context?.GetJobParameter<int>(RetryCountParameter) ?? 0);
}
