namespace MovieApp.Application.Services.Search;

public static class AdvancedDiscoverVoteCountPolicy
{
    public static int? ResolveMinVoteCount(
        int? requestedMinVoteCount,
        bool hasWatchProviders,
        int minVoteCountWhenWatchProvider)
    {
        if (!hasWatchProviders)
        {
            return requestedMinVoteCount;
        }

        var floor = Math.Max(0, minVoteCountWhenWatchProvider);
        if (requestedMinVoteCount is null)
        {
            return floor == 0 ? null : floor;
        }

        return Math.Max(requestedMinVoteCount.Value, floor);
    }
}
