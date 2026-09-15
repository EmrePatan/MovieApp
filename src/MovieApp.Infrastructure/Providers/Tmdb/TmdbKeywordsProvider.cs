using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbKeywordsProvider(TmdbApiClient apiClient) : IKeywordsProvider
{
    private static readonly IReadOnlyList<ProviderKeywordSummary> EmptyKeywords = [];

    public async Task<IReadOnlyList<ProviderKeywordSummary>> GetMovieKeywordsAsync(
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await apiClient.GetAsync<TmdbMovieKeywordsResponseJson>(
                $"movie/{tmdbId}/keywords",
                cancellationToken);

            return response is null
                ? EmptyKeywords
                : TmdbKeywordsMapper.ToProviderKeywords(response.Keywords);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return EmptyKeywords;
        }
    }

    public async Task<IReadOnlyList<ProviderKeywordSummary>> GetTvShowKeywordsAsync(
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await apiClient.GetAsync<TmdbTvKeywordsResponseJson>(
                $"tv/{tmdbId}/keywords",
                cancellationToken);

            return response is null
                ? EmptyKeywords
                : TmdbKeywordsMapper.ToProviderKeywords(response.Results);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return EmptyKeywords;
        }
    }
}
