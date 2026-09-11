using MovieApp.Application.Models.TvShows;

namespace MovieApp.Application.Services.TvShows;

public interface IGetSeasonService
{
    Task<SeasonResult> GetSeasonAsync(
        Guid tvShowId,
        int seasonNumber,
        CancellationToken cancellationToken = default);
}
