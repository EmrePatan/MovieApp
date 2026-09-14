namespace MovieApp.Infrastructure.Configuration;

public sealed class TvShowFollowRateLimitOptions
{
    public const string SectionName = "TvShowFollow:RateLimit";

    public int MutationPermitLimit { get; set; } = 60;

    public int MutationWindowMinutes { get; set; } = 15;
}
