using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Common;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieApp.UnitTests.Search;

public sealed class AutocompleteProviderQueryForwardingTests
{
    [Theory]
    [InlineData("dönersen ı")]
    [InlineData("dönersen I")]
    public async Task ProviderSearchReceivesCollapsedWhitespaceQueryUnmodified(string query)
    {
        var movieProvider = new RecordingMovieDataProvider();
        var service = new UnifiedSearchProviderIngestionService(
            movieProvider,
            new StubTvShowDataProvider(),
            new StubPersonDataProvider(),
            new SearchTestDoubles.FakeLocalizedListDataProvider(),
            new EmptyMovieRepository(),
            new EmptyTvShowRepository(),
            new EmptyPersonRepository(),
            NullLogger<UnifiedSearchProviderIngestionService>.Instance);

        await service.GetAutocompleteSuggestionsAsync(
            query,
            5,
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(QueryNormalizer.CollapseWhitespace(query), movieProvider.LastQuery);
    }

    private sealed class RecordingMovieDataProvider : IMovieDataProvider
    {
        public string? LastQuery { get; private set; }

        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(new MovieProviderSearchResult([], page, pageSize, 0, 0));
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

    private sealed class EmptyMovieRepository : IMovieRepository
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
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(new Dictionary<int, Guid>());
    }

    private sealed class EmptyTvShowRepository : ITvShowRepository
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
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(new Dictionary<int, Guid>());
    }

    private sealed class EmptyPersonRepository : IPersonRepository
    {
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
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(new Dictionary<int, Guid>());
    }

}
