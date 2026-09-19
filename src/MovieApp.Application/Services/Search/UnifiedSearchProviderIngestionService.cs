using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Common;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Services.Search;

public sealed class UnifiedSearchProviderIngestionService(
    IMovieDataProvider movieDataProvider,
    ITvShowDataProvider tvShowDataProvider,
    IPersonDataProvider personDataProvider,
    ILocalizedListDataProvider localizedListDataProvider,
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    IPersonRepository personRepository,
    ILogger<UnifiedSearchProviderIngestionService> logger) : IUnifiedSearchProviderIngestionService
{
    public async Task<UnifiedSearchProviderIngestionResult> IngestAsync(
        SearchCriteria criteria,
        string contentLocale,
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
        var useLocalizedDisplay = ContentLocaleResolver.RequiresLocalization(contentLocale);

        MovieProviderSearchResult? movieSearchResult = null;
        TvShowProviderSearchResult? tvSearchResult = null;
        PersonProviderSearchResult? personSearchResult = null;
        MovieProviderSearchResult? movieIngestResult = null;
        TvShowProviderSearchResult? tvIngestResult = null;
        PersonProviderSearchResult? personIngestResult = null;
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

            var movieTask = useLocalizedDisplay
                ? SearchMoviesLocalizedSafeAsync(query, criteria, contentLocale, cancellationToken)
                : SearchMoviesSafeAsync(query, criteria, cancellationToken);
            var tvTask = useLocalizedDisplay
                ? SearchTvShowsLocalizedSafeAsync(query, criteria, contentLocale, cancellationToken)
                : SearchTvShowsSafeAsync(query, criteria, cancellationToken);
            var personTask = personRequired
                ? useLocalizedDisplay
                    ? SearchPersonsLocalizedSafeAsync(query, criteria, contentLocale, cancellationToken)
                    : SearchPersonsSafeAsync(query, criteria, cancellationToken)
                : Task.FromResult<(PersonProviderSearchResult?, bool)>((null, true));

            Task<(MovieProviderSearchResult? Result, bool Succeeded)>? movieCanonicalTask = null;
            Task<(TvShowProviderSearchResult? Result, bool Succeeded)>? tvCanonicalTask = null;
            Task<(PersonProviderSearchResult? Result, bool Succeeded)>? personCanonicalTask = null;

            if (useLocalizedDisplay)
            {
                movieCanonicalTask = SearchMoviesSafeAsync(query, criteria, cancellationToken);
                tvCanonicalTask = SearchTvShowsSafeAsync(query, criteria, cancellationToken);
                personCanonicalTask = personRequired
                    ? SearchPersonsSafeAsync(query, criteria, cancellationToken)
                    : Task.FromResult<(PersonProviderSearchResult?, bool)>((null, true));

                await Task.WhenAll(movieTask, tvTask, personTask, movieCanonicalTask, tvCanonicalTask, personCanonicalTask);
            }
            else
            {
                await Task.WhenAll(movieTask, tvTask, personTask);
            }

            (movieSearchResult, movieSucceeded) = await movieTask;
            (tvSearchResult, tvSucceeded) = await tvTask;
            (personSearchResult, personSucceeded) = await personTask;

            if (useLocalizedDisplay)
            {
                (movieIngestResult, _) = await movieCanonicalTask!;
                (tvIngestResult, _) = await tvCanonicalTask!;
                (personIngestResult, _) = await personCanonicalTask!;
            }
        }
        else if (movieRequired)
        {
            movieAttempted = true;
            if (useLocalizedDisplay)
            {
                var localizedTask = SearchMoviesLocalizedSafeAsync(query, criteria, contentLocale, cancellationToken);
                var canonicalTask = SearchMoviesSafeAsync(query, criteria, cancellationToken);
                await Task.WhenAll(localizedTask, canonicalTask);
                (movieSearchResult, movieSucceeded) = await localizedTask;
                (movieIngestResult, _) = await canonicalTask;
            }
            else
            {
                (movieSearchResult, movieSucceeded) = await SearchMoviesSafeAsync(query, criteria, cancellationToken);
            }
        }
        else if (tvRequired)
        {
            tvAttempted = true;
            if (useLocalizedDisplay)
            {
                var localizedTask = SearchTvShowsLocalizedSafeAsync(query, criteria, contentLocale, cancellationToken);
                var canonicalTask = SearchTvShowsSafeAsync(query, criteria, cancellationToken);
                await Task.WhenAll(localizedTask, canonicalTask);
                (tvSearchResult, tvSucceeded) = await localizedTask;
                (tvIngestResult, _) = await canonicalTask;
            }
            else
            {
                (tvSearchResult, tvSucceeded) = await SearchTvShowsSafeAsync(query, criteria, cancellationToken);
            }
        }
        else if (personRequired)
        {
            personAttempted = true;
            if (useLocalizedDisplay)
            {
                var localizedTask = SearchPersonsLocalizedSafeAsync(query, criteria, contentLocale, cancellationToken);
                var canonicalTask = SearchPersonsSafeAsync(query, criteria, cancellationToken);
                await Task.WhenAll(localizedTask, canonicalTask);
                (personSearchResult, personSucceeded) = await localizedTask;
                (personIngestResult, _) = await canonicalTask;
            }
            else
            {
                (personSearchResult, personSucceeded) = await SearchPersonsSafeAsync(query, criteria, cancellationToken);
            }
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

        var movieSummariesForIngest = movieIngestResult ?? movieSearchResult;
        var tvSummariesForIngest = tvIngestResult ?? tvSearchResult;
        var personSummariesForIngest = personIngestResult ?? personSearchResult;

        if (movieSummariesForIngest is not null)
        {
            movieIds = await movieRepository.EnsureFromSummariesAsync(
                movieSummariesForIngest.Results,
                cancellationToken);
        }

        if (tvSummariesForIngest is not null)
        {
            tvIds = await tvShowRepository.EnsureFromSummariesAsync(
                tvSummariesForIngest.Results,
                cancellationToken);
        }

        if (personSummariesForIngest is not null)
        {
            personIds = await personRepository.EnsureFromSummariesAsync(
                personSummariesForIngest.Results,
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
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var collapsedQuery = QueryNormalizer.CollapseWhitespace(query);
        var useLocalizedDisplay = ContentLocaleResolver.RequiresLocalization(contentLocale);

        Task<MovieProviderSearchResult> movieSearchTask;
        Task<TvShowProviderSearchResult> tvSearchTask;
        Task<PersonProviderSearchResult> personSearchTask;
        Task<MovieProviderSearchResult>? movieCanonicalTask = null;
        Task<TvShowProviderSearchResult>? tvCanonicalTask = null;
        Task<PersonProviderSearchResult>? personCanonicalTask = null;

        if (useLocalizedDisplay)
        {
            movieSearchTask = localizedListDataProvider.SearchMoviesAsync(
                collapsedQuery,
                1,
                limit,
                contentLocale,
                cancellationToken);
            tvSearchTask = localizedListDataProvider.SearchTvShowsAsync(
                collapsedQuery,
                1,
                limit,
                contentLocale,
                cancellationToken);
            personSearchTask = localizedListDataProvider.SearchPersonsAsync(
                collapsedQuery,
                1,
                limit,
                contentLocale,
                cancellationToken);
            movieCanonicalTask = movieDataProvider.SearchMoviesAsync(collapsedQuery, 1, limit, cancellationToken);
            tvCanonicalTask = tvShowDataProvider.SearchTvShowsAsync(collapsedQuery, 1, limit, cancellationToken);
            personCanonicalTask = personDataProvider.SearchPersonsAsync(collapsedQuery, 1, limit, cancellationToken);
            await Task.WhenAll(
                movieSearchTask,
                tvSearchTask,
                personSearchTask,
                movieCanonicalTask,
                tvCanonicalTask,
                personCanonicalTask);
        }
        else
        {
            movieSearchTask = movieDataProvider.SearchMoviesAsync(collapsedQuery, 1, limit, cancellationToken);
            tvSearchTask = tvShowDataProvider.SearchTvShowsAsync(collapsedQuery, 1, limit, cancellationToken);
            personSearchTask = personDataProvider.SearchPersonsAsync(collapsedQuery, 1, limit, cancellationToken);
            await Task.WhenAll(movieSearchTask, tvSearchTask, personSearchTask);
        }

        var movieSearchResult = await movieSearchTask;
        var tvSearchResult = await tvSearchTask;
        var personSearchResult = await personSearchTask;
        var movieIngestResult = movieCanonicalTask is null
            ? movieSearchResult
            : await movieCanonicalTask;
        var tvIngestResult = tvCanonicalTask is null
            ? tvSearchResult
            : await tvCanonicalTask;
        var personIngestResult = personCanonicalTask is null
            ? personSearchResult
            : await personCanonicalTask;

        var movieIds = await movieRepository.EnsureFromSummariesAsync(
            movieIngestResult.Results,
            cancellationToken);

        var tvIds = await tvShowRepository.EnsureFromSummariesAsync(
            tvIngestResult.Results,
            cancellationToken);

        var personIds = await personRepository.EnsureFromSummariesAsync(
            personIngestResult.Results,
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

    private async Task<(MovieProviderSearchResult? Result, bool Succeeded)> SearchMoviesLocalizedSafeAsync(
        string query,
        SearchCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await localizedListDataProvider.SearchMoviesAsync(
                query,
                criteria.Page,
                criteria.PageSize,
                contentLocale,
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

    private async Task<(TvShowProviderSearchResult? Result, bool Succeeded)> SearchTvShowsLocalizedSafeAsync(
        string query,
        SearchCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await localizedListDataProvider.SearchTvShowsAsync(
                query,
                criteria.Page,
                criteria.PageSize,
                contentLocale,
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

    private async Task<(PersonProviderSearchResult? Result, bool Succeeded)> SearchPersonsLocalizedSafeAsync(
        string query,
        SearchCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        try
        {
            var pageSize = criteria.Type == SearchContentType.All
                ? PersonSearchDefaults.MaxMixedResults
                : criteria.PageSize;

            var result = await localizedListDataProvider.SearchPersonsAsync(
                query,
                criteria.Page,
                pageSize,
                contentLocale,
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
