namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>The "Jobs" configuration section (D-35, D-41).</summary>
public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    /// <summary>
    /// Run a Hangfire server in this process (true in every deployed environment). The integration tests turn it
    /// off and run the jobs themselves, so that nothing processes jobs behind a test's back.
    /// </summary>
    public bool RunServer { get; init; } = true;

    public GlPostingOptions GlPosting { get; init; } = new();
}

public sealed class GlPostingOptions
{
    /// <summary>
    /// Makes the simulated ledger fail every posting, to demonstrate retries, the Failed state,
    /// GL_POSTING_FAILED and the retry endpoint (D-35). Off by default.
    /// </summary>
    public bool SimulateFailure { get; init; }
}
