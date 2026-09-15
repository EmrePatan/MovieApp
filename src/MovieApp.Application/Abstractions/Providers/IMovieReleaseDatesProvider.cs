using MovieApp.Application.Models.RegionalRelease;

namespace MovieApp.Application.Abstractions.Providers;

public interface IMovieReleaseDatesProvider
{
    Task<IReadOnlyList<RegionalMovieReleaseEntry>> GetMovieReleaseDatesAsync(
        int tmdbId,
        CancellationToken cancellationToken = default);
}
