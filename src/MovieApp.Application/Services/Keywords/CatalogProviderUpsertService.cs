using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Keywords;

public sealed class CatalogProviderUpsertService(
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    ICatalogKeywordIngestionService keywordIngestionService,
    IMovieCatalogDetailsCacheInvalidator movieCatalogDetailsCacheInvalidator,
    IContentSearchTitleSynchronizer contentSearchTitleSynchronizer) : ICatalogProviderUpsertService
{
    public async Task<Movie> UpsertMovieFromProviderAsync(
        MovieProviderDetails details,
        bool enrichKeywords = false,
        CancellationToken cancellationToken = default)
    {
        var movie = await movieRepository.UpsertFromProviderAsync(details, cancellationToken);
        await movieCatalogDetailsCacheInvalidator.InvalidateAsync(movie.Id, cancellationToken);
        await contentSearchTitleSynchronizer.SyncFromProviderDetailAsync(
            Domain.Enums.CatalogContentType.Movie,
            movie.Id,
            details.Title,
            details.OriginalTitle,
            details.ProviderSearchTitles,
            DateTime.UtcNow,
            cancellationToken);

        if (enrichKeywords)
        {
            await keywordIngestionService.TryEnrichMovieKeywordsAsync(
                movie.Id,
                refreshKeywords: true,
                details.Keywords,
                cancellationToken);
        }

        return movie;
    }

    public async Task<IReadOnlyList<Movie>> UpsertMoviesFromProviderBatchAsync(
        IReadOnlyList<MovieProviderDetails> details,
        bool enrichKeywords = false,
        CancellationToken cancellationToken = default)
    {
        var movies = await movieRepository.UpsertFromProviderBatchAsync(details, cancellationToken);

        for (var index = 0; index < movies.Count; index++)
        {
            var movie = movies[index];
            await movieCatalogDetailsCacheInvalidator.InvalidateAsync(movie.Id, cancellationToken);
            var detail = details[index];
            await contentSearchTitleSynchronizer.SyncFromProviderDetailAsync(
                Domain.Enums.CatalogContentType.Movie,
                movie.Id,
                detail.Title,
                detail.OriginalTitle,
                detail.ProviderSearchTitles,
                DateTime.UtcNow,
                cancellationToken);
        }

        if (!enrichKeywords)
        {
            return movies;
        }

        for (var index = 0; index < movies.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prefetchedKeywords = index < details.Count ? details[index].Keywords : null;
            await keywordIngestionService.TryEnrichMovieKeywordsAsync(
                movies[index].Id,
                refreshKeywords: true,
                prefetchedKeywords,
                cancellationToken);
        }

        return movies;
    }

    public async Task<TvShow> UpsertTvShowFromProviderAsync(
        TvShowProviderDetails details,
        bool enrichKeywords = false,
        CancellationToken cancellationToken = default)
    {
        var tvShow = await tvShowRepository.UpsertFromProviderAsync(details, cancellationToken);
        await contentSearchTitleSynchronizer.SyncFromProviderDetailAsync(
            Domain.Enums.CatalogContentType.Tv,
            tvShow.Id,
            details.Title,
            details.OriginalTitle,
            details.ProviderSearchTitles,
            DateTime.UtcNow,
            cancellationToken);

        if (enrichKeywords)
        {
            await keywordIngestionService.TryEnrichTvShowKeywordsAsync(
                tvShow.Id,
                refreshKeywords: true,
                details.Keywords,
                cancellationToken);
        }

        return tvShow;
    }

    public async Task<IReadOnlyList<TvShow>> UpsertTvShowsFromProviderBatchAsync(
        IReadOnlyList<TvShowProviderDetails> details,
        bool enrichKeywords = false,
        CancellationToken cancellationToken = default)
    {
        var tvShows = await tvShowRepository.UpsertFromProviderBatchAsync(details, cancellationToken);

        for (var index = 0; index < tvShows.Count; index++)
        {
            var detail = details[index];
            await contentSearchTitleSynchronizer.SyncFromProviderDetailAsync(
                Domain.Enums.CatalogContentType.Tv,
                tvShows[index].Id,
                detail.Title,
                detail.OriginalTitle,
                detail.ProviderSearchTitles,
                DateTime.UtcNow,
                cancellationToken);
        }

        if (!enrichKeywords)
        {
            return tvShows;
        }

        for (var index = 0; index < tvShows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prefetchedKeywords = index < details.Count ? details[index].Keywords : null;
            await keywordIngestionService.TryEnrichTvShowKeywordsAsync(
                tvShows[index].Id,
                refreshKeywords: true,
                prefetchedKeywords,
                cancellationToken);
        }

        return tvShows;
    }
}
