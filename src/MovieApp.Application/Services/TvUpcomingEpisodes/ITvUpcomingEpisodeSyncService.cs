using MovieApp.Application.Models.TvUpcomingEpisodes;

namespace MovieApp.Application.Services.TvUpcomingEpisodes;

public interface ITvUpcomingEpisodeSyncService
{
    Task<TvUpcomingEpisodeSyncBatchResult> RunAsync(CancellationToken cancellationToken = default);
}
