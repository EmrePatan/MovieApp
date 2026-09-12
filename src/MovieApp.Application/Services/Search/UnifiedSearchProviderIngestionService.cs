using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Common;
using MovieApp.Application.Exceptions;
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

        var movieAttempted = false;
        var movieSucceeded = false;
        var tvAttempted = false;
        var tvSucceeded = false;

        if (movieRequired)
        {
            movieAttempted = true;
            movieSucceeded = await TryIngestMoviesAsync(query, criteria.Page, criteria.PageSize, cancellationToken);
        }

        if (tvRequired)
        {
            tvAttempted = true;
            tvSucceeded = await TryIngestTvShowsAsync(query, criteria.Page, criteria.PageSize, cancellationToken);
        }

        return new UnifiedSearchProviderIngestionResult(
            movieRequired,
            tvRequired,
            movieAttempted,
            tvAttempted,
            movieSucceeded,
            tvSucceeded);
    }

    private async Task<bool> TryIngestMoviesAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            await IngestMoviesAsync(query, page, pageSize, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            UnifiedSearchProviderIngestionLogMessages.LogMovieIngestionFailed(logger, query, page, exception);
            return false;
        }
    }

    private async Task<bool> TryIngestTvShowsAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            await IngestTvShowsAsync(query, page, pageSize, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            UnifiedSearchProviderIngestionLogMessages.LogTvIngestionFailed(logger, query, page, exception);
            return false;
        }
    }

    private async Task IngestMoviesAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var providerSearchResult = await movieDataProvider.SearchMoviesAsync(
            query,
            page,
            pageSize,
            cancellationToken);

        foreach (var summary in providerSearchResult.Results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var details = await movieDataProvider.GetMovieAsync(summary.ExternalId, cancellationToken);
            if (details is null)
            {
                continue;
            }

            try
            {
                await movieRepository.UpsertFromProviderAsync(details, cancellationToken);
            }
            catch (MovieExternalIdPersistenceConflictException)
            {
                UnifiedSearchProviderIngestionLogMessages.LogSkippedMoviePersistenceConflict(
                    logger,
                    summary.ExternalId,
                    summary.TmdbId);
            }
        }
    }

    private async Task IngestTvShowsAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var providerSearchResult = await tvShowDataProvider.SearchTvShowsAsync(
            query,
            page,
            pageSize,
            cancellationToken);

        foreach (var summary in providerSearchResult.Results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var details = await tvShowDataProvider.GetTvShowAsync(summary.ExternalId, cancellationToken);
            if (details is null)
            {
                continue;
            }

            await tvShowRepository.UpsertFromProviderAsync(details, cancellationToken);
        }
    }
}
