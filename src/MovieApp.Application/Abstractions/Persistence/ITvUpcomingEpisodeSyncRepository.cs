using MovieApp.Application.Models.TvUpcomingEpisodes;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ITvUpcomingEpisodeSyncRepository
{
    Task<IReadOnlyList<TvUpcomingEpisodeSyncCandidate>> SelectStaleFollowedShowsAsync(
        int batchSize,
        DateTime staleBeforeUtc,
        CancellationToken cancellationToken = default);
}
