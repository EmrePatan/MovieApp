using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Common;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

public sealed class UnifiedSearchProviderIngestionService(
    IMovieDataProvider movieDataProvider,
    ITvShowDataProvider tvShowDataProvider,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    ILogger<UnifiedSearchProviderIngestionService> logger) : IUnifiedSearchProviderIngestionService
{
    public async Task<UnifiedSearchProviderIngestionResult> IngestAsync(
        SearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(criteria.Query))
        {
            return UnifiedSearchProviderIngestionResult.NotRequired();
        }

        var query = QueryNormalizer.CollapseWhitespace(criteria.Query);
        var movieRequired = criteria.Type is SearchContentType.Movie or SearchContentType.All;
        var tvRequired = criteria.Type is SearchContentType.Tv or SearchContentType.All;

        MovieProviderSearchResult? movieSearchResult = null;
        TvShowProviderSearchResult? tvSearchResult = null;
        var movieAttempted = false;
        var movieSucceeded = false;
        var tvAttempted = false;
        var tvSucceeded = false;

        if (movieRequired && tvRequired)
        {
            movieAttempted = true;
            tvAttempted = true;

            var movieTask = SearchMoviesSafeAsync(query, criteria, cancellationToken);
            var tvTask = SearchTvShowsSafeAsync(query, criteria, cancellationToken);
            await Task.WhenAll(movieTask, tvTask);

            (movieSearchResult, movieSucceeded) = await movieTask;
            (tvSearchResult, tvSucceeded) = await tvTask;
        }
        else if (movieRequired)
        {
            movieAttempted = true;
            (movieSearchResult, movieSucceeded) = await SearchMoviesSafeAsync(query, criteria, cancellationToken);
        }
        else if (tvRequired)
        {
            tvAttempted = true;
            (tvSearchResult, tvSucceeded) = await SearchTvShowsSafeAsync(query, criteria, cancellationToken);
        }

        var ingestionResult = new UnifiedSearchProviderIngestionResult(
            movieRequired,
            tvRequired,
            movieAttempted,
            tvAttempted,
            movieSucceeded,
            tvSucceeded);

        if (!ingestionResult.IsFullySuccessful)
        {
            return ingestionResult;
        }

        IReadOnlyDictionary<int, Guid> movieIds = new Dictionary<int, Guid>();
        IReadOnlyDictionary<int, Guid> tvIds = new Dictionary<int, Guid>();

        if (movieSearchResult is not null)
        {
            movieIds = await movieRepository.EnsureFromSummariesAsync(
                movieSearchResult.Results,
                cancellationToken);
        }

        if (tvSearchResult is not null)
        {
            tvIds = await tvShowRepository.EnsureFromSummariesAsync(
                tvSearchResult.Results,
                cancellationToken);
        }

        var result = ProviderSearchMapper.MergeProviderResults(
            criteria,
            movieSearchResult,
            tvSearchResult,
            movieIds,
            tvIds);

        UnifiedSearchProviderIngestionLogMessages.LogProviderSearchSucceeded(
            logger,
            query,
            criteria.Type,
            criteria.Page,
            result.TotalCount);

        return ingestionResult with { Result = result };
    }

    public async Task<IReadOnlyList<SearchSuggestion>> GetAutocompleteSuggestionsAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var collapsedQuery = QueryNormalizer.CollapseWhitespace(query);

        var movieSearchTask = movieDataProvider.SearchMoviesAsync(
            collapsedQuery,
            1,
            limit,
            cancellationToken);
        var tvSearchTask = tvShowDataProvider.SearchTvShowsAsync(
            collapsedQuery,
            1,
            limit,
            cancellationToken);

        await Task.WhenAll(movieSearchTask, tvSearchTask);

        var movieSearchResult = await movieSearchTask;
        var tvSearchResult = await tvSearchTask;

        var movieIds = await movieRepository.EnsureFromSummariesAsync(
            movieSearchResult.Results,
            cancellationToken);

        var tvIds = await tvShowRepository.EnsureFromSummariesAsync(
            tvSearchResult.Results,
            cancellationToken);

        var suggestions = ProviderSearchMapper.MergeAutocompleteSuggestions(
            query,
            movieSearchResult,
            tvSearchResult,
            movieIds,
            tvIds,
            limit);

        UnifiedSearchProviderIngestionLogMessages.LogAutocompleteProviderSucceeded(
            logger,
            collapsedQuery,
            suggestions.Count);

        return suggestions;
    }

    private async Task<(MovieProviderSearchResult? Result, bool Succeeded)> SearchMoviesSafeAsync(
        string query,
        SearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await movieDataProvider.SearchMoviesAsync(
                query,
                criteria.Page,
                criteria.PageSize,
                cancellationToken);
            return (result, true);
        }
        catch (Exception exception)
        {
            UnifiedSearchProviderIngestionLogMessages.LogMovieSearchFailed(
                logger,
                query,
                criteria.Page,
                exception);
            return (null, false);
        }
    }

    private async Task<(TvShowProviderSearchResult? Result, bool Succeeded)> SearchTvShowsSafeAsync(
        string query,
        SearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await tvShowDataProvider.SearchTvShowsAsync(
                query,
                criteria.Page,
                criteria.PageSize,
                cancellationToken);
            return (result, true);
        }
        catch (Exception exception)
        {
            UnifiedSearchProviderIngestionLogMessages.LogTvSearchFailed(
                logger,
                query,
                criteria.Page,
                exception);
            return (null, false);
        }
    }
}
