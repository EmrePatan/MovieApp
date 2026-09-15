using MovieApp.Application.Models.Videos;

namespace MovieApp.Application.Abstractions.Providers;

public interface IVideoProvider
{
    Task<IReadOnlyList<ProviderVideoResult>> GetMovieVideosAsync(
        int tmdbId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProviderVideoResult>> GetTvShowVideosAsync(
        int tmdbId,
        CancellationToken cancellationToken = default);
}
