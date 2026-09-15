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
    IPersonDataProvider personDataProvider,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    IPersonRepository personRepository,
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
        var personRequired = criteria.Type is SearchContentType.Person ||
            (criteria.Type is SearchContentType.All && criteria.Page == 1);
        var personRequiredForSuccess = criteria.Type is SearchContentType.Person;

        MovieProviderSearchResult? movieSearchResult = null;
        TvShowProviderSearchResult? tvSearchResult = null;
        PersonProviderSearchResult? personSearchResult = null;
        var movieAttempted = false;
        var movieSucceeded = false;
        var tvAttempted = false;
        var tvSucceeded = false;
        var personAttempted = false;
        var personSucceeded = false;

        if (criteria.Type == SearchContentType.All)
        {
            movieAttempted = movieRequired;
            tvAttempted = tvRequired;
            personAttempted = personRequired;

            var movieTask = SearchMoviesSafeAsync(query, criteria, cancellationToken);
            var tvTask = SearchTvShowsSafeAsync(query, criteria, cancellationToken);
            var personTask = personRequired
                ? SearchPersonsSafeAsync(query, criteria, cancellationToken)
                : Task.FromResult<(PersonProviderSearchResult?, bool)>((null, true));

            await Task.WhenAll(movieTask, tvTask, personTask);

            (movieSearchResult, movieSucceeded) = await movieTask;
            (tvSearchResult, tvSucceeded) = await tvTask;
            (personSearchResult, personSucceeded) = await personTask;
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
        else if (personRequired)
        {
            personAttempted = true;
            (personSearchResult, personSucceeded) = await SearchPersonsSafeAsync(query, criteria, cancellationToken);
        }

        var ingestionResult = new UnifiedSearchProviderIngestionResult(
            movieRequired,
            tvRequired,
            personRequiredForSuccess,
            movieAttempted,
            tvAttempted,
            personAttempted,
            movieSucceeded,
            tvSucceeded,
            personSucceeded);

        if (!ingestionResult.IsFullySuccessful)
        {
            return ingestionResult;
        }

        IReadOnlyDictionary<int, Guid> movieIds = new Dictionary<int, Guid>();
        IReadOnlyDictionary<int, Guid> tvIds = new Dictionary<int, Guid>();
        IReadOnlyDictionary<int, Guid> personIds = new Dictionary<int, Guid>();

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

        if (personSearchResult is not null)
        {
            personIds = await personRepository.EnsureFromSummariesAsync(
                personSearchResult.Results,
                cancellationToken);
        }

        var result = ProviderSearchMapper.MergeProviderResults(
            criteria,
            movieSearchResult,
            tvSearchResult,
            personSearchResult,
            movieIds,
            tvIds,
            personIds);

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
        var personSearchTask = personDataProvider.SearchPersonsAsync(
            collapsedQuery,
            1,
            limit,
            cancellationToken);

        await Task.WhenAll(movieSearchTask, tvSearchTask, personSearchTask);

        var movieSearchResult = await movieSearchTask;
        var tvSearchResult = await tvSearchTask;
        var personSearchResult = await personSearchTask;

        var movieIds = await movieRepository.EnsureFromSummariesAsync(
            movieSearchResult.Results,
            cancellationToken);

        var tvIds = await tvShowRepository.EnsureFromSummariesAsync(
            tvSearchResult.Results,
            cancellationToken);

        var personIds = await personRepository.EnsureFromSummariesAsync(
            personSearchResult.Results,
            cancellationToken);

        var suggestions = ProviderSearchMapper.MergeAutocompleteSuggestions(
            query,
            movieSearchResult,
            tvSearchResult,
            personSearchResult,
            movieIds,
            tvIds,
            personIds,
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

    private async Task<(PersonProviderSearchResult? Result, bool Succeeded)> SearchPersonsSafeAsync(
        string query,
        SearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        try
        {
            var pageSize = criteria.Type == SearchContentType.All
                ? PersonSearchDefaults.MaxMixedResults
                : criteria.PageSize;

            var result = await personDataProvider.SearchPersonsAsync(
                query,
                criteria.Page,
                pageSize,
                cancellationToken);
            return (result, true);
        }
        catch (Exception exception)
        {
            UnifiedSearchProviderIngestionLogMessages.LogPersonSearchFailed(
                logger,
                query,
                criteria.Page,
                exception);
            return (null, false);
        }
    }
}
