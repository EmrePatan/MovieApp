using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieApp.UnitTests.Search;

public sealed class UnifiedSearchProviderIngestionAutocompleteTests
{
    [Fact]
    public async Task GetAutocompleteSuggestionsAsyncPersistsOnlyRankedCatalogTargets()
    {
        var movieRepository = new TrackingMovieRepository();
        var tvRepository = new TrackingTvShowRepository();
        var personRepository = new TrackingPersonRepository();
        var service = CreateService(
            new ManyMovieDataProvider(),
            new StubTvShowDataProvider(),
            new StubPersonDataProvider(),
            movieRepository,
            tvRepository,
            personRepository);

        var suggestions = await service.GetAutocompleteSuggestionsAsync(
            "jimmy",
            10,
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.True(suggestions.Count <= 10);
        Assert.Equal(10, movieRepository.LastEnsureCount);
        Assert.Equal(0, tvRepository.LastEnsureCount);
        Assert.Equal(0, personRepository.LastEnsureCount);
    }

    [Fact]
    public async Task GetAutocompleteSuggestionsAsyncPropagatesCallerCancellation()
    {
        using var cts = new CancellationTokenSource();
        var service = CreateService(
            new DelayingMovieDataProvider(cts),
            new StubTvShowDataProvider(),
            new StubPersonDataProvider(),
            new TrackingMovieRepository(),
            new TrackingTvShowRepository(),
            new TrackingPersonRepository());

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetAutocompleteSuggestionsAsync(
                "jimmy",
                10,
                ContentLocaleResolver.EnglishUnitedStates,
                cts.Token));
    }

    private static UnifiedSearchProviderIngestionService CreateService(
        IMovieDataProvider movieDataProvider,
        ITvShowDataProvider tvShowDataProvider,
        IPersonDataProvider personDataProvider,
        TrackingMovieRepository movieRepository,
        TrackingTvShowRepository tvShowRepository,
        TrackingPersonRepository personRepository) =>
        new(
            movieDataProvider,
            tvShowDataProvider,
            personDataProvider,
            new SearchTestDoubles.FakeLocalizedListDataProvider(),
            movieRepository,
            tvShowRepository,
            personRepository,
            NullLogger<UnifiedSearchProviderIngestionService>.Instance);

    private sealed class ManyMovieDataProvider : IMovieDataProvider
    {
        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var results = Enumerable.Range(1, 20)
                .Select(index => new MovieProviderSummary(
                    $"tmdb-{index}",
                    index,
                    null,
                    null,
                    index == 1 ? "Jimmy Neutron" : $"Jimmy Title {index}",
                    null,
                    null,
                    null,
                    8m,
                    100))
                .ToList();

            return Task.FromResult(new MovieProviderSearchResult(results, page, pageSize, results.Count, 1));
        }

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderDetails?> GetMovieAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MovieProviderDetails?>(null);
    }

    private sealed class DelayingMovieDataProvider(CancellationTokenSource cancellationTokenSource) : IMovieDataProvider
    {
        public async Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(50, cancellationTokenSource.Token);
            cancellationToken.ThrowIfCancellationRequested();
            return new MovieProviderSearchResult([], page, pageSize, 0, 0);
        }

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderDetails?> GetMovieAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MovieProviderDetails?>(null);
    }

    private sealed class StubTvShowDataProvider : ITvShowDataProvider
    {
        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TvShowProviderSearchResult([], page, pageSize, 0, 0));

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderDetails?> GetTvShowAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShowProviderDetails?>(null);

        public Task<SeasonProviderDetails?> GetSeasonAsync(
            string externalTvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SeasonProviderDetails?>(null);

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(
            string externalTvShowId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EpisodeProviderDetails?>(null);
    }

    private sealed class StubPersonDataProvider : IPersonDataProvider
    {
        public Task<PersonProviderDetails?> GetPersonAsync(int tmdbPersonId, CancellationToken cancellationToken = default) =>
            Task.FromResult<PersonProviderDetails?>(null);

        public Task<PersonProviderSearchResult> SearchPersonsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PersonProviderSearchResult([], page, pageSize, 0, 0));
    }

    private sealed class TrackingMovieRepository : IMovieRepository
    {
        public int LastEnsureCount { get; private set; }

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
            CancellationToken cancellationToken = default)
        {
            LastEnsureCount = summaries.Count;
            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries
                    .Where(summary => summary.TmdbId.HasValue)
                    .ToDictionary(summary => summary.TmdbId!.Value, _ => Guid.NewGuid()));
        }
    }

    private sealed class TrackingTvShowRepository : ITvShowRepository
    {
        public int LastEnsureCount { get; private set; }

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
            CancellationToken cancellationToken = default)
        {
            LastEnsureCount = summaries.Count;
            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries
                    .Where(summary => summary.TmdbId.HasValue)
                    .ToDictionary(summary => summary.TmdbId!.Value, _ => Guid.NewGuid()));
        }
    }

    private sealed class TrackingPersonRepository : IPersonRepository
    {
        public int LastEnsureCount { get; private set; }

        public Task<Person?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Person?>(null);

        public Task<Person> UpsertFromProviderAsync(
            int tmdbId,
            string name,
            string? profilePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Person { Id = Guid.NewGuid(), TmdbId = tmdbId, Name = name });

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<PersonProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            LastEnsureCount = summaries.Count;
            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries.ToDictionary(summary => summary.TmdbId, _ => Guid.NewGuid()));
        }
    }
}
