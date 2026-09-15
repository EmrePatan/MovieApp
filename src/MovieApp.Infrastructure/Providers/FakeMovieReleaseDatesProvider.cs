using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.RegionalRelease;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakeMovieReleaseDatesProvider : IMovieReleaseDatesProvider
{
    public Task<IReadOnlyList<RegionalMovieReleaseEntry>> GetMovieReleaseDatesAsync(
        int tmdbId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RegionalMovieReleaseEntry>>([]);
}
