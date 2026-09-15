using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Keywords;

public sealed class CatalogProviderUpsertService(
    IMovieRepository movieRepository,
    ITvShowRepository tvShowRepository,
    ICatalogKeywordIngestionService keywordIngestionService) : ICatalogProviderUpsertService
{
    public async Task<Movie> UpsertMovieFromProviderAsync(
        MovieProviderDetails details,
        bool enrichKeywords = false,
        CancellationToken cancellationToken = default)
    {
        var movie = await movieRepository.UpsertFromProviderAsync(details, cancellationToken);

        if (enrichKeywords)
        {
            await keywordIngestionService.TryEnrichMovieKeywordsAsync(
                movie.Id,
                refreshKeywords: true,
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

        if (!enrichKeywords)
        {
            return movies;
        }

        foreach (var movie in movies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await keywordIngestionService.TryEnrichMovieKeywordsAsync(
                movie.Id,
                refreshKeywords: true,
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

        if (enrichKeywords)
        {
            await keywordIngestionService.TryEnrichTvShowKeywordsAsync(
                tvShow.Id,
                refreshKeywords: true,
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

        if (!enrichKeywords)
        {
            return tvShows;
        }

        foreach (var tvShow in tvShows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await keywordIngestionService.TryEnrichTvShowKeywordsAsync(
                tvShow.Id,
                refreshKeywords: true,
                cancellationToken);
        }

        return tvShows;
    }
}
