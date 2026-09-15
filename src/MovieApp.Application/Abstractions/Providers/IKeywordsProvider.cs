using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Abstractions.Providers;

public interface IKeywordsProvider
{
    Task<IReadOnlyList<ProviderKeywordSummary>> GetMovieKeywordsAsync(
        int tmdbId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProviderKeywordSummary>> GetTvShowKeywordsAsync(
        int tmdbId,
        CancellationToken cancellationToken = default);
}
