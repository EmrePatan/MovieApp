using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Movies;

internal sealed class NullMovieDataProvider : IMovieDataProvider
{
    public Task<MovieProviderDetails?> GetMovieAsync(
        string externalId,
        bool includeKeywords = false,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<MovieProviderDetails?>(null);

    public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
        DiscoverProviderCriteria criteria,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<MovieProviderSearchResult> SearchMoviesAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class StubMovieDataProvider(MovieProviderDetails? details) : IMovieDataProvider
{
    public Task<MovieProviderDetails?> GetMovieAsync(
        string externalId,
        bool includeKeywords = false,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(details);

    public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
        DiscoverProviderCriteria criteria,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<MovieProviderSearchResult> SearchMoviesAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class RecordingCatalogProviderUpsertService(Func<MovieProviderDetails, Movie> upsert)
    : ICatalogProviderUpsertService
{
    public MovieProviderDetails? LastDetails { get; private set; }

    public Task<Movie> UpsertMovieFromProviderAsync(
        MovieProviderDetails details,
        bool enrichKeywords = false,
        CancellationToken cancellationToken = default)
    {
        LastDetails = details;
        return Task.FromResult(upsert(details));
    }

    public Task<IReadOnlyList<Movie>> UpsertMoviesFromProviderBatchAsync(
        IReadOnlyList<MovieProviderDetails> details,
        bool enrichKeywords = false,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TvShow> UpsertTvShowFromProviderAsync(
        TvShowProviderDetails details,
        bool enrichKeywords = false,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<TvShow>> UpsertTvShowsFromProviderBatchAsync(
        IReadOnlyList<TvShowProviderDetails> details,
        bool enrichKeywords = false,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class NoOpCatalogProviderUpsertService : ICatalogProviderUpsertService
{
    public Task<Movie> UpsertMovieFromProviderAsync(
        MovieProviderDetails details,
        bool enrichKeywords = false,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Upsert should not be called.");

    public Task<IReadOnlyList<Movie>> UpsertMoviesFromProviderBatchAsync(
        IReadOnlyList<MovieProviderDetails> details,
        bool enrichKeywords = false,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<TvShow> UpsertTvShowFromProviderAsync(
        TvShowProviderDetails details,
        bool enrichKeywords = false,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<TvShow>> UpsertTvShowsFromProviderBatchAsync(
        IReadOnlyList<TvShowProviderDetails> details,
        bool enrichKeywords = false,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
