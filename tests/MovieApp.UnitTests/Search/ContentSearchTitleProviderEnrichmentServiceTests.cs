using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.UnitTests.Search;

public sealed class ContentSearchTitleProviderEnrichmentServiceTests
{
    [Fact]
    public async Task EnrichRespectsMaxItemsAndResumeCursor()
    {
        var movieIds = new[] { Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") };
        var repository = new FakeEnrichmentRepository(
            movies:
            [
                new ContentSearchTitleEnrichmentCandidate(CatalogContentType.Movie, movieIds[0], FakeMovieDataProvider.InterstellarTmdbId, "One"),
                new ContentSearchTitleEnrichmentCandidate(CatalogContentType.Movie, movieIds[1], FakeMovieDataProvider.PosterlessTmdbId, "Two"),
            ],
            tvShows: []);

        var upsert = new RecordingUpsertService();
        var service = CreateService(repository, upsert);

        var result = await service.EnrichFromProviderAsync(new ContentSearchTitleProviderEnrichmentRequest
        {
            ContentType = CatalogContentType.Movie,
            MaxItems = 1,
            DelayBetweenRequestsMs = 0,
        });

        Assert.Equal(1, result.Processed);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(movieIds[0], result.LastProcessedMovieId);
        Assert.Single(upsert.MovieDetails);
    }

    [Fact]
    public async Task EnrichContinuesAfterProviderFailure()
    {
        var movieIds = new[] { Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") };
        var repository = new FakeEnrichmentRepository(
            movies:
            [
                new ContentSearchTitleEnrichmentCandidate(CatalogContentType.Movie, movieIds[0], 999, "Fail"),
                new ContentSearchTitleEnrichmentCandidate(CatalogContentType.Movie, movieIds[1], FakeMovieDataProvider.InterstellarTmdbId, "Ok"),
            ],
            tvShows: []);

        var upsert = new RecordingUpsertService();
        var provider = new FakeMovieDataProvider(new MovieDataProviderCallTracker());
        var service = CreateService(repository, upsert, provider);

        var result = await service.EnrichFromProviderAsync(new ContentSearchTitleProviderEnrichmentRequest
        {
            ContentType = CatalogContentType.Movie,
            MaxItems = 2,
            DelayBetweenRequestsMs = 0,
        });

        Assert.Equal(2, result.Processed);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(1, result.Skipped);
        Assert.Single(upsert.MovieDetails);
    }

    [Fact]
    public async Task EnrichTvShowUsesProviderUpsertPath()
    {
        var tvId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var repository = new FakeEnrichmentRepository(
            movies: [],
            tvShows:
            [
                new ContentSearchTitleEnrichmentCandidate(CatalogContentType.Tv, tvId, 500, "Tv"),
            ]);

        var upsert = new RecordingUpsertService();
        var tvShow = new TvShow
        {
            Id = tvId,
            Title = "Tv",
            TmdbId = 500,
            FirstAirDate = new DateOnly(2020, 1, 1),
            VoteAverage = 1,
            VoteCount = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var service = new ContentSearchTitleProviderEnrichmentService(
            repository,
            new FakeMovieDataProvider(new MovieDataProviderCallTracker()),
            new StubTvShowDataProvider(),
            new FakeTvShowRepository(tvShow),
            new FakeTvShowExternalIdResolver(),
            upsert,
            NullLogger<ContentSearchTitleProviderEnrichmentService>.Instance);

        var result = await service.EnrichFromProviderAsync(new ContentSearchTitleProviderEnrichmentRequest
        {
            ContentType = CatalogContentType.Tv,
            MaxItems = 1,
            DelayBetweenRequestsMs = 0,
        });

        Assert.Equal(1, result.Succeeded);
        Assert.Single(upsert.TvShowDetails);
    }

    [Fact]
    public async Task EnrichResumeSkipsEarlierMovieIds()
    {
        var movieIds = new[]
        {
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        };
        var repository = new FakeEnrichmentRepository(
            movies:
            [
                new ContentSearchTitleEnrichmentCandidate(CatalogContentType.Movie, movieIds[0], FakeMovieDataProvider.InterstellarTmdbId, "One"),
                new ContentSearchTitleEnrichmentCandidate(CatalogContentType.Movie, movieIds[1], FakeMovieDataProvider.PosterlessTmdbId, "Two"),
            ],
            tvShows: []);

        var upsert = new RecordingUpsertService();
        var service = CreateService(repository, upsert);

        var result = await service.EnrichFromProviderAsync(new ContentSearchTitleProviderEnrichmentRequest
        {
            ContentType = CatalogContentType.Movie,
            StartAfterMovieId = movieIds[0],
            MaxItems = 1,
            DelayBetweenRequestsMs = 0,
        });

        Assert.Equal(1, result.Succeeded);
        Assert.Equal(movieIds[1], result.LastProcessedMovieId);
        Assert.Single(upsert.MovieDetails);
        Assert.Equal(FakeMovieDataProvider.PosterlessTmdbId, upsert.MovieDetails[0].TmdbId);
    }

    [Fact]
    public void ProviderEnrichmentIsNotRegisteredAsHostedStartupWork()
    {
        var apiRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "MovieApp.Api"));
        var programPath = Path.Combine(apiRoot, "Program.cs");
        Assert.True(File.Exists(programPath));
        var programSource = File.ReadAllText(programPath);
        Assert.DoesNotContain("ContentSearchTitleProviderEnrichment", programSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IContentSearchTitleProviderEnrichmentService", programSource, StringComparison.Ordinal);
    }

    private static ContentSearchTitleProviderEnrichmentService CreateService(
        IContentSearchTitleProviderEnrichmentRepository repository,
        RecordingUpsertService upsert,
        IMovieDataProvider? movieDataProvider = null) =>
        new(
            repository,
            movieDataProvider ?? new FakeMovieDataProvider(new MovieDataProviderCallTracker()),
            new FakeTvShowDataProvider(),
            new FakeTvShowRepository(),
            new FakeTvShowExternalIdResolver(),
            upsert,
            NullLogger<ContentSearchTitleProviderEnrichmentService>.Instance);

    private sealed class FakeEnrichmentRepository(
        IReadOnlyList<ContentSearchTitleEnrichmentCandidate> movies,
        IReadOnlyList<ContentSearchTitleEnrichmentCandidate> tvShows) : IContentSearchTitleProviderEnrichmentRepository
    {
        public Task<int> CountMoviesWithTmdbIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(movies.Count);

        public Task<int> CountTvShowsWithResolvableProviderIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(tvShows.Count);

        public Task<IReadOnlyList<ContentSearchTitleEnrichmentCandidate>> SelectMovieCandidatesAsync(
            Guid? startAfterId,
            Guid? onlyMovieId,
            int take,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<ContentSearchTitleEnrichmentCandidate> query = movies;
            if (onlyMovieId is not null)
            {
                query = query.Where(candidate => candidate.ContentId == onlyMovieId);
            }
            else if (startAfterId is not null)
            {
                query = query.Where(candidate => candidate.ContentId.CompareTo(startAfterId.Value) > 0);
            }

            return Task.FromResult<IReadOnlyList<ContentSearchTitleEnrichmentCandidate>>(query.Take(take).ToList());
        }

        public Task<IReadOnlyList<ContentSearchTitleEnrichmentCandidate>> SelectTvShowCandidatesAsync(
            Guid? startAfterId,
            Guid? onlyTvShowId,
            int take,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContentSearchTitleEnrichmentCandidate>>(tvShows.Take(take).ToList());

        public Task<Guid?> FindMovieIdByTitleAsync(string title, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(null);

        public Task<Guid?> FindMovieIdByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(null);
    }

    private sealed class RecordingUpsertService : ICatalogProviderUpsertService
    {
        public List<MovieProviderDetails> MovieDetails { get; } = [];

        public Task<Movie> UpsertMovieFromProviderAsync(
            MovieProviderDetails details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default)
        {
            MovieDetails.Add(details);
            return Task.FromResult(new Movie { Id = Guid.NewGuid(), Title = details.Title });
        }

        public Task<IReadOnlyList<Movie>> UpsertMoviesFromProviderBatchAsync(
            IReadOnlyList<MovieProviderDetails> details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public List<TvShowProviderDetails> TvShowDetails { get; } = [];

        public Task<TvShow> UpsertTvShowFromProviderAsync(
            TvShowProviderDetails details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default)
        {
            TvShowDetails.Add(details);
            return Task.FromResult(new TvShow { Id = Guid.NewGuid(), Title = details.Title });
        }

        public Task<IReadOnlyList<TvShow>> UpsertTvShowsFromProviderBatchAsync(
            IReadOnlyList<TvShowProviderDetails> details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTvShowDataProvider : ITvShowDataProvider
    {
        public Task<TvShowProviderDetails?> GetTvShowAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShowProviderDetails?>(null);

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

        public Task<TvShowProviderSearchResult> AdvancedDiscoverTvShowsAsync(
            AdvancedDiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SeasonProviderDetails?> GetSeasonAsync(
            string externalId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(
            string externalId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubTvShowDataProvider : ITvShowDataProvider
    {
        public Task<TvShowProviderDetails?> GetTvShowAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShowProviderDetails?>(new TvShowProviderDetails(
                "tmdb-500",
                500,
                null,
                null,
                "Tv Title",
                "Tv Original",
                "Overview",
                new DateOnly(2020, 1, 1),
                null,
                null,
                null,
                "en",
                7m,
                10,
                "Returning Series",
                ["Drama"],
                [],
                null));

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

        public Task<TvShowProviderSearchResult> AdvancedDiscoverTvShowsAsync(
            AdvancedDiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SeasonProviderDetails?> GetSeasonAsync(
            string externalId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(
            string externalId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTvShowRepository(TvShow? tvShow = null) : ITvShowRepository
    {
        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(tvShow is not null && tvShow.Id == id ? tvShow : null);

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(null);

        public Task<IReadOnlyDictionary<Guid, TvShow>> GetByIdsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, TvShow>>(new Dictionary<Guid, TvShow>());

        public Task<TvShow> UpsertFromProviderAsync(TvShowProviderDetails details, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TvShow>> UpsertFromProviderBatchAsync(
            IReadOnlyList<TvShowProviderDetails> details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTvShowExternalIdResolver : ITvShowExternalIdResolver
    {
        public string? Resolve(int? tmdbId, int? tvdbId, string? imdbId) =>
            tmdbId?.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
