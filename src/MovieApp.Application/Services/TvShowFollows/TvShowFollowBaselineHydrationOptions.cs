namespace MovieApp.Application.Services.TvShowFollows;

internal static class TvShowFollowBaselineHydrationOptions
{
    public const int MaxConcurrentSeasonFetches = 3;

    public const int MaxSeasonsPerPersistenceBatch = 3;
}
