using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Movies;
using MovieApp.Domain.Entities;
using MovieApp.UnitTests.Keywords;
using MovieApp.UnitTests.Search;

namespace MovieApp.UnitTests.Movies;

public sealed class SearchMoviesServiceProviderAmplificationTests
{
    [Fact]
    public async Task SearchAsyncCapsProviderDetailRequestsToConfiguredMaximum()
    {
        var provider = new CountingMovieDataProvider(CreateSummaries(100));
        var service = new SearchMoviesService(
            provider,
            CatalogProviderUpsertTestDoubles.CreateRepositoryBackedUpsertService(new NoOpMovieRepository()),
            new SearchServiceTestsHelper.FakeCacheService(null),
            Options.Create(new SearchOptions
            {
                MaxProviderDetailFetchesPerContentType = 20,
                MaxConcurrentProviderHttpRequests = 4
            }),
            NullLogger<SearchMoviesService>.Instance);

        await service.SearchAsync(new MovieSearchRequest("batman", 1, 100));

        Assert.Equal(1, provider.SearchCallCount);
        Assert.Equal(20, provider.DetailCallCount);
        Assert.Equal(21, provider.TotalProviderHttpCalls);
    }

    private static List<MovieProviderSummary> CreateSummaries(int count) =>
        Enumerable.Range(1, count)
            .Select(index => new MovieProviderSummary(
                $"fake-{index}",
                index,
                null,
                $"tt{index:D7}",
                $"Movie {index}",
                "Overview",
                new DateOnly(2020, 1, 1),
                "/poster.jpg",
                7.0m,
                100))
            .ToList();

    private sealed class CountingMovieDataProvider(IReadOnlyList<MovieProviderSummary> summaries) : IMovieDataProvider
    {
        public int SearchCallCount { get; private set; }

        public int DetailCallCount { get; private set; }

        public int TotalProviderHttpCalls => SearchCallCount + DetailCallCount;

        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            SearchCallCount++;
            return Task.FromResult(new MovieProviderSearchResult(
                summaries,
                page,
                pageSize,
                summaries.Count,
                1));
        }

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderDetails?> GetMovieAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default)
        {
            DetailCallCount++;
            var summary = summaries.Single(item => string.Equals(item.ExternalId, externalId, StringComparison.Ordinal));
            return Task.FromResult<MovieProviderDetails?>(new MovieProviderDetails(
                summary.ExternalId,
                summary.TmdbId,
                summary.TvdbId,
                summary.ImdbId,
                summary.Title,
                summary.Title,
                summary.Overview,
                summary.ReleaseDate,
                100,
                summary.PosterPath,
                "/backdrop.jpg",
                "en",
                summary.VoteAverage,
                summary.VoteCount,
                ["Drama"]));
        }
    }

    private sealed class NoOpMovieRepository : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie> UpsertFromProviderAsync(MovieProviderDetails details, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Movie
            {
                Id = Guid.NewGuid(),
                TmdbId = details.TmdbId,
                Title = details.Title
            });

        public async Task<IReadOnlyList<Movie>> UpsertFromProviderBatchAsync(
            IReadOnlyList<MovieProviderDetails> details,
            CancellationToken cancellationToken = default)
        {
            var movies = new List<Movie>(details.Count);

            foreach (var detail in details)
            {
                movies.Add(await UpsertFromProviderAsync(detail, cancellationToken));
            }

            return movies;
        }
    }
}
