using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Search;

public sealed class LocalizedSearchCanonicalIngestTests
{
    [Fact]
    public async Task IngestAsync_DoesNotStoreLocalizedTitles_WhenCanonicalSearchFails()
    {
        var movies = new RecordingMovieRepository();
        var service = new UnifiedSearchProviderIngestionService(
            new ThrowingMovieDataProvider(),
            new ThrowingTvShowDataProvider(),
            new ThrowingPersonDataProvider(),
            new TurkishMovieListProvider(),
            movies,
            new ThrowingTvShowRepository(),
            new ThrowingPersonRepository(),
            NullLogger<UnifiedSearchProviderIngestionService>.Instance);

        var result = await service.IngestAsync(
            new SearchCriteria(
                "parazit",
                SearchContentType.Movie,
                null,
                null,
                null,
                null,
                SearchSortOption.Relevance,
                1,
                20),
            ContentLocaleResolver.TurkishTurkey);

        Assert.False(result.IsFullySuccessful);
        Assert.Null(result.Result);
        Assert.Empty(movies.IngestedTitles);
    }

    private sealed class TurkishMovieListProvider : ILocalizedListDataProvider
    {
        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MovieProviderSearchResult(
                [new MovieProviderSummary("tmdb-496243", 496243, null, null, "Parazit", "Özet", null, null, 8m, 10, "기생충", OriginalLanguage: "ko")],
                page,
                pageSize,
                1,
                1));

        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PersonProviderSearchResult> SearchPersonsAsync(
            string query,
            int page,
            int pageSize,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
            DiscoverProviderCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderSearchResult> AdvancedDiscoverMoviesAsync(
            AdvancedDiscoverProviderCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> AdvancedDiscoverTvShowsAsync(
            AdvancedDiscoverProviderCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingMovieDataProvider : IMovieDataProvider
    {
        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("canonical search failed");

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderDetails?> GetMovieAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingTvShowDataProvider : ITvShowDataProvider
    {
        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderDetails?> GetTvShowAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SeasonProviderDetails?> GetSeasonAsync(
            string externalTvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(
            string externalTvShowId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingPersonDataProvider : IPersonDataProvider
    {
        public Task<PersonProviderDetails?> GetPersonAsync(int tmdbPersonId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PersonProviderSearchResult> SearchPersonsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingMovieRepository : IMovieRepository
    {
        public List<string> IngestedTitles { get; } = [];

        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            IngestedTitles.AddRange(summaries.Select(summary => summary.Title));
            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(new Dictionary<int, Guid>());
        }
    }

    private sealed class ThrowingTvShowRepository : ITvShowRepository
    {
        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingPersonRepository : IPersonRepository
    {
        public Task<Person?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Person> UpsertFromProviderAsync(
            int tmdbId,
            string name,
            string? profilePath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<PersonProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
