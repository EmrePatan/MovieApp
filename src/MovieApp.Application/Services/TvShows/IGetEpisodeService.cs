using MovieApp.Application.Models.TvShows;

namespace MovieApp.Application.Services.TvShows;

public interface IGetEpisodeService
{
    Task<EpisodeResult> GetEpisodeAsync(
        Guid tvShowId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default);
}
