using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbMovieDataProvider(TmdbApiClient apiClient) : IMovieDataProvider
{
    public async Task<MovieProviderSearchResult> SearchMoviesAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var encodedQuery = Uri.EscapeDataString(query.Trim());
        var response = await apiClient.GetCanonicalAsync<TmdbMovieSearchResponseJson>(
            $"search/movie?query={encodedQuery}&include_adult=false&page={page}",
            cancellationToken);

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

    public async Task<MovieProviderSearchResult> DiscoverMoviesAsync(
        DiscoverProviderCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(criteria);
        var response = await apiClient.GetCanonicalAsync<TmdbMovieSearchResponseJson>(
            $"discover/movie?{query}",
            cancellationToken);

        if (response is null)
        {
            return new MovieProviderSearchResult(
                [],
                criteria.Page,
                TmdbSearchDefaults.ResultsPerPage,
                0,
                0);
        }

        var results = response.Results
            .Select(TmdbMovieMapper.ToSummary)
            .ToList();

        return new MovieProviderSearchResult(
            results,
            response.Page == 0 ? criteria.Page : response.Page,
            TmdbSearchDefaults.ResultsPerPage,
            response.TotalResults,
            response.TotalPages);
    }

    public async Task<MovieProviderSearchResult> AdvancedDiscoverMoviesAsync(
        AdvancedDiscoverProviderCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = TmdbAdvancedDiscoverQueryBuilder.BuildMovieQuery(criteria);
        var response = await apiClient.GetCanonicalAsync<TmdbMovieSearchResponseJson>(
            $"discover/movie?{query}",
            cancellationToken);

        if (response is null)
        {
            return new MovieProviderSearchResult(
                [],
                criteria.Page,
                TmdbSearchDefaults.ResultsPerPage,
                0,
                0);
        }

        var results = response.Results
            .Select(TmdbMovieMapper.ToSummary)
            .ToList();

        return new MovieProviderSearchResult(
            results,
            response.Page == 0 ? criteria.Page : response.Page,
            TmdbSearchDefaults.ResultsPerPage,
            response.TotalResults,
            response.TotalPages);
    }

    public async Task<MovieProviderDetails?> GetMovieAsync(
        string externalId,
        bool includeKeywords = false,
        CancellationToken cancellationToken = default)
    {
        if (!TmdbExternalIdFormatter.TryParseExternalId(externalId, out var tmdbId))
        {
            return null;
        }

        try
        {
            var response = await apiClient.GetCanonicalAsync<TmdbMovieDetailsResponseJson>(
                BuildMovieDetailsPath(tmdbId, includeKeywords),
                cancellationToken);

            return response is null ? null : TmdbMovieMapper.ToDetails(response);
        }
        catch (TmdbApiException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static string BuildMovieDetailsPath(int tmdbId, bool includeKeywords)
    {
        var appendToResponse = includeKeywords ? "external_ids,keywords" : "external_ids";
        return $"movie/{tmdbId}?append_to_response={appendToResponse}";
    }
}
