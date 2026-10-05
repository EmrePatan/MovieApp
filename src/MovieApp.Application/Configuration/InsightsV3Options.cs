namespace MovieApp.Application.Configuration;

public sealed class InsightsV3Options
{
    public const string SectionName = "InsightsV3";

    public const int DefaultMaxRepositoryConcurrency = 11;

    public const int V3RepositoryPhaseCount = 11;

    /// <summary>
    /// Maximum number of Insights V3 repository phases that may execute PostgreSQL commands concurrently
    /// within a single <c>GetV3RawDataAsync</c> call.
    /// </summary>
    public int MaxRepositoryConcurrency { get; set; } = DefaultMaxRepositoryConcurrency;
}
