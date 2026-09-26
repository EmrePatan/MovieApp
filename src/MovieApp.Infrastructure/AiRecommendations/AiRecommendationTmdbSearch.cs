using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.AiRecommendations;

internal sealed class AiRecommendationTmdbSearch(TmdbApiClient apiClient) : IAiRecommendationTmdbSearch
{
    public async Task<MovieProviderSearchResult> SearchMoviesAsync(
        string query,
        int page,
        int pageSize,
        string? language,
        CancellationToken cancellationToken = default)
    {
        var encodedQuery = Uri.EscapeDataString(query.Trim());
        var relativePath = $"search/movie?query={encodedQuery}&include_adult=false&page={page}";

        var response = string.IsNullOrWhiteSpace(language)
            ? await apiClient.GetCanonicalAsync<TmdbMovieSearchResponseJson>(relativePath, cancellationToken)
            : await apiClient.GetLocalizedAsync<TmdbMovieSearchResponseJson>(relativePath, language, cancellationToken);

        if (response is null)
        {
            return new MovieProviderSearchResult(
                [],
                page,
                TmdbSearchDefaults.ResultsPerPage,
                0,
                0);
        }

        var results = response.Results
            .Select(TmdbMovieMapper.ToSummary)
            .ToList();

        return new MovieProviderSearchResult(
            results,
            response.Page,
            TmdbSearchDefaults.ResultsPerPage,
            response.TotalResults,
            response.TotalPages);
    }

    public async Task<TvShowProviderSearchResult> SearchTvShowsAsync(
        string query,
        int page,
        int pageSize,
        string? language,
        CancellationToken cancellationToken = default)
    {
        var encodedQuery = Uri.EscapeDataString(query.Trim());
        var relativePath = $"search/tv?query={encodedQuery}&include_adult=false&page={page}";

        var response = string.IsNullOrWhiteSpace(language)
            ? await apiClient.GetCanonicalAsync<TmdbTvSearchResponseJson>(relativePath, cancellationToken)
            : await apiClient.GetLocalizedAsync<TmdbTvSearchResponseJson>(relativePath, language, cancellationToken);

        if (response is null)
        {
            return new TvShowProviderSearchResult(
                [],
                page,
                TmdbSearchDefaults.ResultsPerPage,
                0,
                0);
        }

        var results = response.Results
            .Select(TmdbTvShowMapper.ToSummary)
            .ToList();

        return new TvShowProviderSearchResult(
            results,
            response.Page,
            TmdbSearchDefaults.ResultsPerPage,
            response.TotalResults,
            response.TotalPages);
    }
}
