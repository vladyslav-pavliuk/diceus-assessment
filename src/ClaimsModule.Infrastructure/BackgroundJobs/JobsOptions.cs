namespace ClaimsModule.Infrastructure.BackgroundJobs;

public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    /// <summary>Integration tests turn it off and run jobs themselves, so nothing runs behind a test's back.</summary>
    public bool RunServer { get; init; } = true;

    public GlPostingOptions GlPosting { get; init; } = new();
}

public sealed class GlPostingOptions
{
    /// <summary>Makes every posting fail, to demo retries and the Failed state (D-35).</summary>
    public bool SimulateFailure { get; init; }
}
