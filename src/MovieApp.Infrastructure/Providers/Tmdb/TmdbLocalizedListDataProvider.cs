using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbLocalizedListDataProvider(TmdbApiClient apiClient) : ILocalizedListDataProvider
{
    public async Task<MovieProviderSearchResult> SearchMoviesAsync(
        string query,
        int page,
        int pageSize,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var encodedQuery = Uri.EscapeDataString(query.Trim());
        var response = await apiClient.GetLocalizedAsync<TmdbMovieSearchResponseJson>(
            $"search/movie?query={encodedQuery}&include_adult=false&page={page}",
            contentLocale,
            cancellationToken);

        return MapMovieSearchResponse(response, page);
    }

    public async Task<TvShowProviderSearchResult> SearchTvShowsAsync(
        string query,
        int page,
        int pageSize,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var encodedQuery = Uri.EscapeDataString(query.Trim());
        var response = await apiClient.GetLocalizedAsync<TmdbTvSearchResponseJson>(
            $"search/tv?query={encodedQuery}&include_adult=false&page={page}",
            contentLocale,
            cancellationToken);

        return MapTvSearchResponse(response, page);
    }

    public async Task<PersonProviderSearchResult> SearchPersonsAsync(
        string query,
        int page,
        int pageSize,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var encodedQuery = Uri.EscapeDataString(query.Trim());
        var response = await apiClient.GetLocalizedAsync<TmdbPersonSearchResponseJson>(
            $"search/person?query={encodedQuery}&include_adult=false&page={page}",
            contentLocale,
            cancellationToken);

        if (response is null)
        {
            return new PersonProviderSearchResult(
                [],
                page,
                TmdbSearchDefaults.ResultsPerPage,
                0,
                0);
        }

        var results = response.Results
            .Where(result => result.Id > 0 && !string.IsNullOrWhiteSpace(result.Name))
            .Select(TmdbPersonMapper.ToSummary)
            .ToList();

        return new PersonProviderSearchResult(
            results,
            response.Page,
            TmdbSearchDefaults.ResultsPerPage,
            response.TotalResults,
            response.TotalPages);
    }

    public async Task<MovieProviderSearchResult> DiscoverMoviesAsync(
        DiscoverProviderCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var query = TmdbDiscoverQueryBuilder.BuildMovieQuery(criteria);
        var response = await apiClient.GetLocalizedAsync<TmdbMovieSearchResponseJson>(
            $"discover/movie?{query}",
            contentLocale,
            cancellationToken);

        return MapMovieSearchResponse(response, criteria.Page, criteria.Page);
    }

    public async Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
        DiscoverProviderCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var query = TmdbDiscoverQueryBuilder.BuildTvQuery(criteria);
        var response = await apiClient.GetLocalizedAsync<TmdbTvSearchResponseJson>(
            $"discover/tv?{query}",
            contentLocale,
            cancellationToken);

        return MapTvSearchResponse(response, criteria.Page, criteria.Page);
    }

    public async Task<MovieProviderSearchResult> AdvancedDiscoverMoviesAsync(
        AdvancedDiscoverProviderCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var query = TmdbAdvancedDiscoverQueryBuilder.BuildMovieQuery(criteria);
        var response = await apiClient.GetLocalizedAsync<TmdbMovieSearchResponseJson>(
            $"discover/movie?{query}",
            contentLocale,
            cancellationToken);

        return MapMovieSearchResponse(response, criteria.Page, criteria.Page);
    }

    public async Task<TvShowProviderSearchResult> AdvancedDiscoverTvShowsAsync(
        AdvancedDiscoverProviderCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var query = TmdbAdvancedDiscoverQueryBuilder.BuildTvQuery(criteria);
        var response = await apiClient.GetLocalizedAsync<TmdbTvSearchResponseJson>(
            $"discover/tv?{query}",
            contentLocale,
            cancellationToken);

        return MapTvSearchResponse(response, criteria.Page, criteria.Page);
    }

    private static MovieProviderSearchResult MapMovieSearchResponse(
        TmdbMovieSearchResponseJson? response,
        int page,
        int fallbackPage = 0)
    {
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
            response.Page == 0 ? (fallbackPage == 0 ? page : fallbackPage) : response.Page,
            TmdbSearchDefaults.ResultsPerPage,
            response.TotalResults,
            response.TotalPages);
    }

    private static TvShowProviderSearchResult MapTvSearchResponse(
        TmdbTvSearchResponseJson? response,
        int page,
        int fallbackPage = 0)
    {
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
            response.Page == 0 ? (fallbackPage == 0 ? page : fallbackPage) : response.Page,
            TmdbSearchDefaults.ResultsPerPage,
            response.TotalResults,
            response.TotalPages);
    }
}
