namespace MovieApp.Application.Services.TvShows;

public interface ITvShowSeasonSummaryHydrator
{
    Task<TvShowSeasonSummaryHydrationResult> EnsureSeasonSummariesAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default);
}
