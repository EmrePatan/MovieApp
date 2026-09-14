using MovieApp.Application.Models.Credits;

namespace MovieApp.Application.Abstractions.Providers;

public interface ICreditsProvider
{
    Task<CreditsResult> GetMovieCreditsAsync(int tmdbId, CancellationToken cancellationToken = default);

    Task<CreditsResult> GetTvShowCreditsAsync(int tmdbId, CancellationToken cancellationToken = default);
}
