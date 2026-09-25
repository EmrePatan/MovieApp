using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Search;

public sealed class UnifiedSearchCatalogIngestParallelTests
{
    [Fact]
    public async Task EnsureCatalogIdsAsync_WritesMovieTvAndPeopleInParallelScopes()
    {
        var gate = new IngestGate();
        var service = new UnifiedSearchProviderIngestionService(
            new ThrowingProviders(),
            new ThrowingProviders(),
            new ThrowingProviders(),
            new SearchTestDoubles.FakeLocalizedListDataProvider(),
            new ThrowingMovieRepository(),
            new ThrowingTvShowRepository(),
            new ThrowingPersonRepository(),
            NullLogger<UnifiedSearchProviderIngestionService>.Instance,
            new IngestScopeFactory(gate));

        var movies = await service.EnsureCatalogIdsAsync(
            [new MovieProviderSummary("m", 1, null, null, "Movie", null, null, null, 1m, 1)],
            [new TvShowProviderSummary("t", 2, null, null, "Show", null, null, null, null, null, null, 1m, 1)],
            [new PersonProviderSummary(3, "Person", null, null, 1m)],
            CancellationToken.None);

        Assert.Equal(3, movies.Movies.Count + movies.TvShows.Count + movies.People.Count);
        Assert.True(gate.MaxInFlight >= 2);
    }

    private sealed class IngestGate
    {
        private int _inFlight;

        public int MaxInFlight { get; private set; }

        public async Task EnterAsync()
        {
            var current = Interlocked.Increment(ref _inFlight);
            lock (this)
            {
                if (current > MaxInFlight)
                {
                    MaxInFlight = current;
                }
            }

            try
            {
                await Task.Delay(40);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }
    }

    private sealed class IngestScopeFactory(IngestGate gate) : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IMovieRepository>(new GatedMovieRepository(gate));
            services.AddSingleton<ITvShowRepository>(new GatedTvShowRepository(gate));
            services.AddSingleton<IPersonRepository>(new GatedPersonRepository(gate));
            return services.BuildServiceProvider().CreateScope();
        }
    }

    private sealed class GatedMovieRepository(IngestGate gate) : ThrowingMovieRepository
    {
        public override async Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            await gate.EnterAsync();
            return summaries.Where(summary => summary.TmdbId.HasValue)
                .ToDictionary(summary => summary.TmdbId!.Value, _ => Guid.NewGuid());
        }
    }

    private sealed class GatedTvShowRepository(IngestGate gate) : ThrowingTvShowRepository
    {
        public override async Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            await gate.EnterAsync();
            return summaries.Where(summary => summary.TmdbId.HasValue)
                .ToDictionary(summary => summary.TmdbId!.Value, _ => Guid.NewGuid());
        }
    }

    private sealed class GatedPersonRepository(IngestGate gate) : ThrowingPersonRepository
    {
        public override async Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<PersonProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            await gate.EnterAsync();
            return summaries.ToDictionary(summary => summary.TmdbId, _ => Guid.NewGuid());
        }
    }

    private class ThrowingMovieRepository : IMovieRepository
    {
        public virtual Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie> UpsertFromProviderAsync(MovieProviderDetails details, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private class ThrowingTvShowRepository : ITvShowRepository
    {
        public virtual Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow> UpsertFromProviderAsync(TvShowProviderDetails details, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private class ThrowingPersonRepository : IPersonRepository
    {
        public virtual Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<PersonProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Person?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Person> UpsertFromProviderAsync(
            int tmdbId,
            string name,
            string? profilePath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingProviders : IMovieDataProvider, ITvShowDataProvider, IPersonDataProvider
    {
        public Task<MovieProviderSearchResult> SearchMoviesAsync(string query, int page, int pageSize, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(DiscoverProviderCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderDetails?> GetMovieAsync(string externalId, bool includeKeywords = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(string query, int page, int pageSize, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(DiscoverProviderCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderDetails?> GetTvShowAsync(string externalId, bool includeKeywords = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SeasonProviderDetails?> GetSeasonAsync(string externalTvShowId, int seasonNumber, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(string externalTvShowId, int seasonNumber, int episodeNumber, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PersonProviderDetails?> GetPersonAsync(int tmdbPersonId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PersonProviderSearchResult> SearchPersonsAsync(string query, int page, int pageSize, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
