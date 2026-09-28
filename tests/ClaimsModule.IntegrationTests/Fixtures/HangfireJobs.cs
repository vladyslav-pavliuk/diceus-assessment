using ClaimsModule.Infrastructure.BackgroundJobs;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.Fixtures;

/// <summary>Reads what the real Hangfire adapter wrote to the real storage of a test host (no server runs in tests).</summary>
internal static class HangfireJobs
{
    /// <summary>The PostGLReserveChangeJob jobs enqueued for one reserve transaction, as (job id, arguments).</summary>
    public static IReadOnlyList<(string JobId, IReadOnlyList<object?> Args)> EnqueuedGlPostings(IServiceProvider services, Guid reserveHistoryId)
    {
        var monitoring = services.GetRequiredService<JobStorage>().GetMonitoringApi();
        return monitoring.EnqueuedJobs("default", 0, 10_000)
            .Select(pair => (pair.Key, Job: pair.Value.Job))
            .Where(job => job.Job?.Type == typeof(PostGLReserveChangeJob)
                && job.Job.Method.Name == nameof(PostGLReserveChangeJob.ExecuteAsync)
                && Equals(job.Job.Args[0], reserveHistoryId))
            .Select(job => (job.Key, (IReadOnlyList<object?>)job.Job!.Args.ToList()))
            .ToList();
    }

    public static IReadOnlyList<RecurringJobDto> RecurringJobs(IServiceProvider services)
    {
        using var connection = services.GetRequiredService<JobStorage>().GetConnection();
        return connection.GetRecurringJobs();
    }
}
