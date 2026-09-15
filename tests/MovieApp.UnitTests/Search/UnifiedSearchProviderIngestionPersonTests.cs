using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Providers;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieApp.UnitTests.Search;

public sealed class UnifiedSearchProviderIngestionPersonTests
{
    [Fact]
    public async Task IngestAsyncAllTypeContinuesWhenPersonSearchFails()
    {
        var service = CreateService(
            new ThrowingPersonDataProvider(),
            new StubMovieDataProvider(),
            new StubTvShowDataProvider());

        var criteria = new SearchCriteria(
            "keanu",
            SearchContentType.All,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);

        var result = await service.IngestAsync(criteria);

        Assert.True(result.IsFullySuccessful);
        Assert.NotNull(result.Result);
        Assert.Contains(result.Result!.Items, item => item.Type == "movie");
        Assert.Contains(result.Result.Items, item => item.Type == "tv");
        Assert.DoesNotContain(result.Result.Items, item => item.Type == "person");
    }

    [Fact]
    public async Task IngestAsyncPersonTypeFailsWhenPersonSearchFails()
    {
        var service = CreateService(
            new ThrowingPersonDataProvider(),
            new StubMovieDataProvider(),
            new StubTvShowDataProvider());

        var criteria = new SearchCriteria(
            "keanu",
            SearchContentType.Person,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);

        var result = await service.IngestAsync(criteria);

        Assert.False(result.IsFullySuccessful);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task IngestAsyncPersonTypeReturnsPersonResults()
    {
        var service = CreateService(
            new FakePersonDataProvider(),
            new StubMovieDataProvider(),
            new StubTvShowDataProvider());

        var criteria = new SearchCriteria(
            "keanu",
            SearchContentType.Person,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);

        var result = await service.IngestAsync(criteria);

        Assert.True(result.IsFullySuccessful);
        Assert.NotNull(result.Result);
        Assert.All(result.Result!.Items, item => Assert.Equal("person", item.Type));
        Assert.Contains(result.Result.Items, item => item.TmdbId == FakePersonDataProvider.KeanuReevesTmdbId);
    }

    [Fact]
    public async Task IngestAsyncAllPageOneIncludesLimitedPersonResults()
    {
        var service = CreateService(
            new FakePersonDataProvider(),
            new StubMovieDataProvider(),
            new StubTvShowDataProvider());

        var criteria = new SearchCriteria(
            "keanu",
            SearchContentType.All,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);

        var result = await service.IngestAsync(criteria);

        Assert.True(result.IsFullySuccessful);
        Assert.NotNull(result.Result);
        Assert.Contains(result.Result!.Items, item => item.Type == "person");
        Assert.True(result.Result.Items.Count(item => item.Type == "person") <= PersonSearchDefaults.MaxMixedResults);
    }

    private static UnifiedSearchProviderIngestionService CreateService(
        IPersonDataProvider personDataProvider,
        IMovieDataProvider movieDataProvider,
        ITvShowDataProvider tvShowDataProvider) =>
        new(
            movieDataProvider,
            tvShowDataProvider,
            personDataProvider,
            new InMemoryMovieRepository(),
            new InMemoryTvShowRepository(),
            new InMemoryPersonRepository(),
            NullLogger<UnifiedSearchProviderIngestionService>.Instance);

    private sealed class ThrowingPersonDataProvider : IPersonDataProvider
    {
        public Task<PersonProviderDetails?> GetPersonAsync(int tmdbPersonId, CancellationToken cancellationToken = default) =>
            Task.FromResult<PersonProviderDetails?>(null);

        public Task<PersonProviderSearchResult> SearchPersonsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("person provider unavailable");
    }

    private sealed class StubMovieDataProvider : IMovieDataProvider
    {
        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MovieProviderSearchResult(
                [new("tmdb-1", 1, null, null, "Stub Movie", null, null, null, 8m, 100)],
                page,
                pageSize,
                1,
                1));

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderDetails?> GetMovieAsync(string externalId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MovieProviderDetails?>(null);
    }

    private sealed class StubTvShowDataProvider : ITvShowDataProvider
    {
        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TvShowProviderSearchResult(
                [new("tmdb-2", 2, null, null, "Stub Show", null, null, null, null, null, null, 7m, 50)],
                page,
                pageSize,
                1,
                1));

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderDetails?> GetTvShowAsync(string externalId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShowProviderDetails?>(null);

        public Task<SeasonProviderDetails?> GetSeasonAsync(
            string externalId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SeasonProviderDetails?>(null);

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(
            string externalId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EpisodeProviderDetails?>(null);
    }

    private sealed class InMemoryPersonRepository : IPersonRepository
    {
        public Task<Person?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Person?>(null);

        public Task<Person> UpsertFromProviderAsync(
            int tmdbId,
            string name,
            string? profilePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Person
            {
                Id = Guid.NewGuid(),
                TmdbId = tmdbId,
                Name = name,
                ProfilePath = profilePath
            });

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<PersonProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries.ToDictionary(summary => summary.TmdbId, _ => Guid.NewGuid()));
    }

    private sealed class InMemoryMovieRepository : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Movie { Id = Guid.NewGuid() });

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries
                    .Where(summary => summary.TmdbId.HasValue)
                    .ToDictionary(summary => summary.TmdbId!.Value, _ => Guid.NewGuid()));
    }

    private sealed class InMemoryTvShowRepository : ITvShowRepository
    {
        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(null);

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(null);

        public Task<TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TvShow { Id = Guid.NewGuid() });

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries
                    .Where(summary => summary.TmdbId.HasValue)
                    .ToDictionary(summary => summary.TmdbId!.Value, _ => Guid.NewGuid()));
    }
}
