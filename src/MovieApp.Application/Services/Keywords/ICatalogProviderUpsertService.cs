using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Keywords;

public interface ICatalogProviderUpsertService
{
    Task<Movie> UpsertMovieFromProviderAsync(
        MovieProviderDetails details,
        bool enrichKeywords = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Movie>> UpsertMoviesFromProviderBatchAsync(
        IReadOnlyList<MovieProviderDetails> details,
        bool enrichKeywords = false,
        CancellationToken cancellationToken = default);

    Task<TvShow> UpsertTvShowFromProviderAsync(
        TvShowProviderDetails details,
        bool enrichKeywords = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TvShow>> UpsertTvShowsFromProviderBatchAsync(
        IReadOnlyList<TvShowProviderDetails> details,
        bool enrichKeywords = false,
        CancellationToken cancellationToken = default);
}
