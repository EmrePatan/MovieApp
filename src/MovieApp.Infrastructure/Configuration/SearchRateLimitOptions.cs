namespace MovieApp.Infrastructure.Configuration;

public sealed class SearchRateLimitOptions
{
    public const string SectionName = "Search:RateLimit";

    public int UnifiedSearchPermitLimit { get; set; } = 30;

    public int UnifiedSearchWindowMinutes { get; set; } = 1;

    public int MovieSearchPermitLimit { get; set; } = 20;

    public int MovieSearchWindowMinutes { get; set; } = 1;

    public int TvSearchPermitLimit { get; set; } = 20;

    public int TvSearchWindowMinutes { get; set; } = 1;
}
