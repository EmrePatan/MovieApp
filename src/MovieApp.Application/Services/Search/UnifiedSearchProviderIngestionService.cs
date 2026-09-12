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
    public async Task IngestAsync(SearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(criteria.Query))
        {
            return;
        }

        var query = QueryNormalizer.CollapseWhitespace(criteria.Query);

        if (criteria.Type is SearchContentType.Movie or SearchContentType.All)
        {
            await IngestMoviesAsync(query, criteria.Page, criteria.PageSize, cancellationToken);
        }

        if (criteria.Type is SearchContentType.Tv or SearchContentType.All)
        {
            await IngestTvShowsAsync(query, criteria.Page, criteria.PageSize, cancellationToken);
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
