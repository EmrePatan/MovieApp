using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.WatchHistory;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Services.TvShows;
using MovieApp.Domain.Enums;
using MovieApp.Application.Services.WatchHistory;
using MovieApp.Domain.Entities;
using MovieApp.UnitTests.Caching;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieApp.UnitTests.WatchHistory;

public sealed class WatchHistoryServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly Guid MovieId = Guid.NewGuid();
    private static readonly Guid TvShowId = Guid.NewGuid();
    private static readonly Guid SeasonId = Guid.NewGuid();
    private static readonly Guid EpisodeId1 = Guid.NewGuid();
    private static readonly Guid EpisodeId2 = Guid.NewGuid();
    private static readonly Guid EpisodeId3 = Guid.NewGuid();

    [Fact]
    public async Task MarkMovieWatchedAsyncCreatesRecord()
    {
        var watchedMovieRepo = new FakeWatchedMovieRepository();
        var service = CreateService(watchedMovieRepo, new FakeWatchedEpisodeRepository());

        var result = await service.MarkMovieWatchedAsync(MovieId);

        Assert.True(result.Created);
        Assert.Equal(1, watchedMovieRepo.UpsertCount);
    }

    [Fact]
    public async Task MarkMovieWatchedAsyncIsIdempotent()
    {
        var watchedMovieRepo = new FakeWatchedMovieRepository(upsertReturnsCreated: false);
        var service = CreateService(watchedMovieRepo, new FakeWatchedEpisodeRepository());

        var result = await service.MarkMovieWatchedAsync(MovieId);

        Assert.False(result.Created);
    }

    [Fact]
    public async Task MarkMovieWatchedAsyncThrowsWhenMovieMissing()
    {
        var service = new WatchHistoryService(
            new FakeCurrentUser(UserId),
            new FakeWatchedMovieRepository(),
            new FakeWatchedEpisodeRepository(),
            new FakeMovieRepository(null),
            new FakeEpisodeRepository(CreateEpisode(EpisodeId1, 1, 1, "Pilot"), 7, CreateEpisode(EpisodeId1, 1, 1, "Pilot"), 3, CreateEpisode(EpisodeId1, 1, 1, "Pilot")),
            new FakeTvShowRepository(CreateTvShow()),
            new FakeSeasonRepository(CreateSeason()),
            new FakeGetSeasonService(),
            new FakeSeasonSummaryHydrator(),
            new FakeCatalogSyncStateService(),
            new FakeUserAnalyticsCacheInvalidator(),
            NullLogger<WatchHistoryService>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(() => service.MarkMovieWatchedAsync(MovieId));
    }

    [Fact]
    public async Task UnmarkMovieWatchedAsyncRemovesRecord()
    {
        var watchedMovieRepo = new FakeWatchedMovieRepository();
        var service = CreateService(watchedMovieRepo, new FakeWatchedEpisodeRepository());

        await service.UnmarkMovieWatchedAsync(MovieId);

        Assert.Equal(1, watchedMovieRepo.RemoveCount);
    }

    [Fact]
    public async Task GetMovieWatchStatusAsyncReturnsWatched()
    {
        var watchedAt = DateTime.UtcNow;
        var watchedMovieRepo = new FakeWatchedMovieRepository(
            existingMovie: WatchedMovie.Create(UserId, MovieId, watchedAt));
        var service = CreateService(watchedMovieRepo, new FakeWatchedEpisodeRepository());

        var result = await service.GetMovieWatchStatusAsync(MovieId);

        Assert.True(result.IsWatched);
        Assert.Equal(watchedAt, result.WatchedAt);
    }

    [Fact]
    public async Task GetMovieWatchStatusAsyncReturnsNotWatched()
    {
        var service = CreateService(new FakeWatchedMovieRepository(), new FakeWatchedEpisodeRepository());

        var result = await service.GetMovieWatchStatusAsync(MovieId);

        Assert.False(result.IsWatched);
        Assert.Null(result.WatchedAt);
    }

    [Fact]
    public async Task MarkEpisodeWatchedAsyncCreatesRecord()
    {
        var watchedEpisodeRepo = new FakeWatchedEpisodeRepository();
        var service = CreateService(new FakeWatchedMovieRepository(), watchedEpisodeRepo);

        var result = await service.MarkEpisodeWatchedAsync(EpisodeId1);

        Assert.True(result.Created);
        Assert.Equal(1, watchedEpisodeRepo.UpsertCount);
    }

    [Fact]
    public async Task MarkEpisodeWatchedAsyncIsIdempotent()
    {
        var watchedEpisodeRepo = new FakeWatchedEpisodeRepository(upsertReturnsCreated: false);
        var service = CreateService(new FakeWatchedMovieRepository(), watchedEpisodeRepo);

        var result = await service.MarkEpisodeWatchedAsync(EpisodeId1);

        Assert.False(result.Created);
    }

    [Fact]
    public async Task MarkEpisodeWatchedAsyncThrowsWhenEpisodeMissing()
    {
        var service = new WatchHistoryService(
            new FakeCurrentUser(UserId),
            new FakeWatchedMovieRepository(),
            new FakeWatchedEpisodeRepository(),
            new FakeMovieRepository(CreateMovie()),
            new FakeEpisodeRepository(null, 7, CreateEpisode(EpisodeId1, 1, 1, "Pilot"), 3, CreateEpisode(EpisodeId1, 1, 1, "Pilot")),
            new FakeTvShowRepository(CreateTvShow()),
            new FakeSeasonRepository(CreateSeason()),
            new FakeGetSeasonService(),
            new FakeSeasonSummaryHydrator(),
            new FakeCatalogSyncStateService(),
            new FakeUserAnalyticsCacheInvalidator(),
            NullLogger<WatchHistoryService>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(() => service.MarkEpisodeWatchedAsync(EpisodeId1));
    }

    [Fact]
    public async Task GetEpisodeWatchStatusAsyncReturnsWatched()
    {
        var watchedAt = DateTime.UtcNow;
        var watchedEpisodeRepo = new FakeWatchedEpisodeRepository(
            existingEpisode: WatchedEpisode.Create(UserId, EpisodeId1, watchedAt));
        var service = CreateService(new FakeWatchedMovieRepository(), watchedEpisodeRepo);

        var result = await service.GetEpisodeWatchStatusAsync(EpisodeId1);

        Assert.True(result.IsWatched);
        Assert.Equal(watchedAt, result.WatchedAt);
    }

    [Fact]
    public async Task GetEpisodeWatchStatusAsyncReturnsNotWatched()
    {
        var service = CreateService(new FakeWatchedMovieRepository(), new FakeWatchedEpisodeRepository());

        var result = await service.GetEpisodeWatchStatusAsync(EpisodeId1);

        Assert.False(result.IsWatched);
        Assert.Null(result.WatchedAt);
    }

    [Fact]
    public async Task GetWatchedMoviesAsyncUsesPaginationDefaults()
    {
        var watchedMovieRepo = new FakeWatchedMovieRepository(totalMovies: 45);
        var service = CreateService(watchedMovieRepo, new FakeWatchedEpisodeRepository());

        var result = await service.GetWatchedMoviesAsync(1, 20);

        Assert.Equal(20, result.PageSize);
        Assert.Equal(45, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public async Task GetWatchedMoviesAsyncThrowsForInvalidPagination()
    {
        var service = CreateService(new FakeWatchedMovieRepository(), new FakeWatchedEpisodeRepository());

        await Assert.ThrowsAsync<ValidationException>(() => service.GetWatchedMoviesAsync(0, 20));
    }

    [Fact]
    public async Task GetTvShowWatchProgressAsyncReturnsZeroWhenNoEpisodes()
    {
        var service = new WatchHistoryService(
            new FakeCurrentUser(UserId),
            new FakeWatchedMovieRepository(),
            new FakeWatchedEpisodeRepository(),
            new FakeMovieRepository(CreateMovie()),
            new FakeEpisodeRepository(CreateEpisode(EpisodeId1, 1, 1, "Pilot"), 0, null, 0, null),
            new FakeTvShowRepository(CreateTvShow()),
            new FakeSeasonRepository(CreateSeason()),
            new FakeGetSeasonService(),
            new FakeSeasonSummaryHydrator(),
            new FakeCatalogSyncStateService(),
            new FakeUserAnalyticsCacheInvalidator(),
            NullLogger<WatchHistoryService>.Instance);

        var result = await service.GetTvShowWatchProgressAsync(TvShowId);

        Assert.Equal(0, result.TotalEpisodes);
        Assert.Equal(0, result.WatchedEpisodes);
        Assert.Equal(0m, result.ProgressPercentage);
        Assert.Null(result.NextEpisode);
        Assert.Empty(result.Seasons);
    }

    [Fact]
    public async Task GetTvShowWatchProgressAsyncReturnsPartialProgress()
    {
        var service = CreateService(
            new FakeWatchedMovieRepository(),
            new FakeWatchedEpisodeRepository(watchedForTvShow: 2),
            totalEpisodes: 7,
            nextEpisode: CreateEpisode(EpisodeId3, 1, 3, "Third"));

        var result = await service.GetTvShowWatchProgressAsync(TvShowId);

        Assert.Equal(7, result.TotalEpisodes);
        Assert.Equal(2, result.WatchedEpisodes);
        Assert.Equal(28.57m, result.ProgressPercentage);
        Assert.NotNull(result.NextEpisode);
        Assert.Equal(3, result.NextEpisode!.EpisodeNumber);
    }

    [Fact]
    public async Task GetTvShowWatchProgressAsyncReturnsCompleteProgress()
    {
        var service = new WatchHistoryService(
            new FakeCurrentUser(UserId),
            new FakeWatchedMovieRepository(),
            new FakeWatchedEpisodeRepository(watchedForTvShow: 7),
            new FakeMovieRepository(CreateMovie()),
            new FakeEpisodeRepository(CreateEpisode(EpisodeId1, 1, 1, "Pilot"), 7, null, 3, null),
            new FakeTvShowRepository(CreateTvShow()),
            new FakeSeasonRepository(CreateSeason()),
            new FakeGetSeasonService(),
            new FakeSeasonSummaryHydrator(),
            new FakeCatalogSyncStateService(),
            new FakeUserAnalyticsCacheInvalidator(),
            NullLogger<WatchHistoryService>.Instance);

        var result = await service.GetTvShowWatchProgressAsync(TvShowId);

        Assert.Equal(100m, result.ProgressPercentage);
        Assert.Null(result.NextEpisode);
    }

    [Fact]
    public async Task GetSeasonWatchProgressAsyncReturnsPartialProgress()
    {
        var service = CreateService(
            new FakeWatchedMovieRepository(),
            new FakeWatchedEpisodeRepository(watchedForSeason: 2),
            totalSeasonEpisodes: 3,
            nextSeasonEpisode: CreateEpisode(EpisodeId3, 1, 3, "Third"),
            season: CreateSeason());

        var result = await service.GetSeasonWatchProgressAsync(TvShowId, 1);

        Assert.Equal(3, result.TotalEpisodes);
        Assert.Equal(2, result.WatchedEpisodes);
        Assert.Equal(66.67m, result.ProgressPercentage);
        Assert.NotNull(result.NextEpisode);
        Assert.Equal(3, result.NextEpisode!.EpisodeNumber);
    }

    [Fact]
    public async Task GetSeasonWatchProgressAsyncThrowsWhenSeasonMissing()
    {
        var service = new WatchHistoryService(
            new FakeCurrentUser(UserId),
            new FakeWatchedMovieRepository(),
            new FakeWatchedEpisodeRepository(),
            new FakeMovieRepository(CreateMovie()),
            new FakeEpisodeRepository(CreateEpisode(EpisodeId1, 1, 1, "Pilot"), 7, CreateEpisode(EpisodeId1, 1, 1, "Pilot"), 3, CreateEpisode(EpisodeId1, 1, 1, "Pilot")),
            new FakeTvShowRepository(CreateTvShow()),
            new FakeSeasonRepository(null),
            new FakeGetSeasonService(),
            new FakeSeasonSummaryHydrator(),
            new FakeCatalogSyncStateService(),
            new FakeUserAnalyticsCacheInvalidator(),
            NullLogger<WatchHistoryService>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetSeasonWatchProgressAsync(TvShowId, 99));
    }

    [Fact]
    public async Task GetSeasonWatchProgressAsyncThrowsForInvalidSeasonNumber()
    {
        var service = CreateService(new FakeWatchedMovieRepository(), new FakeWatchedEpisodeRepository());

        await Assert.ThrowsAsync<ValidationException>(() => service.GetSeasonWatchProgressAsync(TvShowId, 0));
    }

    [Fact]
    public async Task GetRecentWatchHistoryAsyncMergesMoviesAndEpisodes()
    {
        var watchedMovieRepo = new FakeWatchedMovieRepository(
            recentMovies:
            [
                (MovieId, "Interstellar", DateTime.UtcNow.AddHours(-1))
            ],
            totalMovies: 1);
        var watchedEpisodeRepo = new FakeWatchedEpisodeRepository(
            recentEpisodes:
            [
                (EpisodeId1, TvShowId, SeasonId, "Breaking Bad", 1, 1, "Pilot", DateTime.UtcNow)
            ],
            totalEpisodes: 1);
        var service = CreateService(watchedMovieRepo, watchedEpisodeRepo);

        var result = await service.GetRecentWatchHistoryAsync(1, 20);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("episode", result.Items[0].Type);
        Assert.Equal("movie", result.Items[1].Type);
    }

    [Fact]
    public async Task BulkUpdateEpisodeWatchStateAsyncMarksEpisodes()
    {
        var watchedEpisodeRepo = new FakeWatchedEpisodeRepository();
        var service = CreateService(new FakeWatchedMovieRepository(), watchedEpisodeRepo);

        var result = await service.BulkUpdateEpisodeWatchStateAsync(
            TvShowId,
            [EpisodeId1, EpisodeId2],
            watched: true);

        Assert.Equal(2, result.AffectedCount);
        Assert.NotNull(result.WatchedAt);
        Assert.Equal(1, watchedEpisodeRepo.BulkMarkCount);
    }

    [Fact]
    public async Task BulkUpdateEpisodeWatchStateAsyncUnmarksEpisodes()
    {
        var watchedEpisodeRepo = new FakeWatchedEpisodeRepository();
        var service = CreateService(new FakeWatchedMovieRepository(), watchedEpisodeRepo);

        var result = await service.BulkUpdateEpisodeWatchStateAsync(
            TvShowId,
            [EpisodeId1, EpisodeId2],
            watched: false);

        Assert.Equal(2, result.AffectedCount);
        Assert.Null(result.WatchedAt);
        Assert.Equal(1, watchedEpisodeRepo.BulkUnmarkCount);
    }

    [Fact]
    public async Task BulkUpdateEpisodeWatchStateAsyncRejectsEmptyEpisodeList()
    {
        var service = CreateService(new FakeWatchedMovieRepository(), new FakeWatchedEpisodeRepository());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.BulkUpdateEpisodeWatchStateAsync(TvShowId, [], watched: true));
    }

    [Fact]
    public async Task MarkThroughEpisodeAsyncMarksEpisodesUpToTarget()
    {
        var watchedEpisodeRepo = new FakeWatchedEpisodeRepository();
        var service = CreateService(new FakeWatchedMovieRepository(), watchedEpisodeRepo);

        var result = await service.MarkThroughEpisodeAsync(TvShowId, EpisodeId1);

        Assert.Equal(EpisodeId1, result.EpisodeId);
        Assert.Equal(1, result.AffectedCount);
        Assert.Equal(1, watchedEpisodeRepo.BulkMarkCount);
    }

    [Fact]
    public async Task MarkMovieWatchedAsyncThrowsWhenUserNotAuthenticated()
    {
        var service = CreateService(
            new FakeWatchedMovieRepository(),
            new FakeWatchedEpisodeRepository(),
            currentUser: new FakeCurrentUser(null));

        await Assert.ThrowsAsync<AuthenticationException>(() => service.MarkMovieWatchedAsync(MovieId));
    }

    private static WatchHistoryService CreateService(
        FakeWatchedMovieRepository watchedMovieRepository,
        FakeWatchedEpisodeRepository watchedEpisodeRepository,
        Movie? movie = null,
        Episode? episode = null,
        int totalEpisodes = 7,
        Episode? nextEpisode = null,
        int totalSeasonEpisodes = 3,
        Episode? nextSeasonEpisode = null,
        Season? season = null,
        ICurrentUser? currentUser = null) =>
        new(
            currentUser ?? new FakeCurrentUser(UserId),
            watchedMovieRepository,
            watchedEpisodeRepository,
            new FakeMovieRepository(movie ?? CreateMovie()),
            new FakeEpisodeRepository(
                episode ?? CreateEpisode(EpisodeId1, 1, 1, "Pilot"),
                totalEpisodes,
                nextEpisode ?? CreateEpisode(EpisodeId1, 1, 1, "Pilot"),
                totalSeasonEpisodes,
                nextSeasonEpisode ?? CreateEpisode(EpisodeId1, 1, 1, "Pilot")),
            new FakeTvShowRepository(CreateTvShow()),
            new FakeSeasonRepository(season ?? CreateSeason()),
            new FakeGetSeasonService(),
            new FakeSeasonSummaryHydrator(),
            new FakeCatalogSyncStateService(),
            new FakeUserAnalyticsCacheInvalidator(),
            NullLogger<WatchHistoryService>.Instance);

    private static Movie CreateMovie() =>
        new()
        {
            Id = MovieId,
            Title = "Interstellar",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static TvShow CreateTvShow() =>
        new()
        {
            Id = TvShowId,
            Title = "Breaking Bad",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static Season CreateSeason() =>
        new()
        {
            Id = SeasonId,
            TvShowId = TvShowId,
            SeasonNumber = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static Episode CreateEpisode(Guid id, int seasonNumber, int episodeNumber, string name) =>
        new()
        {
            Id = id,
            SeasonId = SeasonId,
            EpisodeNumber = episodeNumber,
            Name = name,
            Season = new Season
            {
                Id = SeasonId,
                TvShowId = TvShowId,
                SeasonNumber = seasonNumber,
                TvShow = CreateTvShow(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId is not null;

        public Guid? UserId => userId;
    }

    private sealed class FakeWatchedMovieRepository : IWatchedMovieRepository
    {
        private readonly WatchedMovie? _existingMovie;
        private readonly bool _upsertReturnsCreated;
        private readonly IReadOnlyList<(Guid MovieId, string Title, DateTime WatchedAt)> _recentMovies;
        private readonly int _totalMovies;

        public FakeWatchedMovieRepository(
            WatchedMovie? existingMovie = null,
            bool upsertReturnsCreated = true,
            IReadOnlyList<(Guid MovieId, string Title, DateTime WatchedAt)>? recentMovies = null,
            int totalMovies = 0)
        {
            _existingMovie = existingMovie;
            _upsertReturnsCreated = upsertReturnsCreated;
            _recentMovies = recentMovies ?? [];
            _totalMovies = totalMovies;
        }

        public int UpsertCount { get; private set; }

        public int RemoveCount { get; private set; }

        public Task<WatchedMovie?> GetByUserAndMovieAsync(
            Guid userId,
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_existingMovie);

        public Task<(WatchedMovie Entity, bool Created)> UpsertAsync(
            WatchedMovie watchedMovie,
            CancellationToken cancellationToken = default)
        {
            UpsertCount++;
            return Task.FromResult((watchedMovie, _upsertReturnsCreated));
        }

        public Task<bool> RemoveAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default)
        {
            RemoveCount++;
            return Task.FromResult(true);
        }

        public Task<(IReadOnlyList<WatchedMovie> Items, int TotalCount)> GetUserWatchedMoviesAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var movie = CreateMovie();
            var watchedMovie = WatchedMovie.Create(userId, movie.Id, DateTime.UtcNow);
            watchedMovie.Movie = movie;
            return Task.FromResult<(IReadOnlyList<WatchedMovie>, int)>(([watchedMovie], _totalMovies));
        }

        public Task<IReadOnlyList<(Guid MovieId, string Title, DateTime WatchedAt)>> GetRecentForUserAsync(
            Guid userId,
            int take,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_recentMovies);

        public Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_totalMovies);
    }

    private sealed class FakeWatchedEpisodeRepository : IWatchedEpisodeRepository
    {
        private readonly WatchedEpisode? _existingEpisode;
        private readonly bool _upsertReturnsCreated;
        private readonly int _watchedForTvShow;
        private readonly int _watchedForSeason;
        private readonly IReadOnlyList<(
            Guid EpisodeId,
            Guid TvShowId,
            Guid SeasonId,
            string TvShowTitle,
            int SeasonNumber,
            int EpisodeNumber,
            string? EpisodeTitle,
            DateTime WatchedAt)> _recentEpisodes;
        private readonly int _totalEpisodes;

        public FakeWatchedEpisodeRepository(
            WatchedEpisode? existingEpisode = null,
            bool upsertReturnsCreated = true,
            int watchedForTvShow = 0,
            int watchedForSeason = 0,
            IReadOnlyList<(
                Guid EpisodeId,
                Guid TvShowId,
                Guid SeasonId,
                string TvShowTitle,
                int SeasonNumber,
                int EpisodeNumber,
                string? EpisodeTitle,
                DateTime WatchedAt)>? recentEpisodes = null,
            int totalEpisodes = 0)
        {
            _existingEpisode = existingEpisode;
            _upsertReturnsCreated = upsertReturnsCreated;
            _watchedForTvShow = watchedForTvShow;
            _watchedForSeason = watchedForSeason;
            _recentEpisodes = recentEpisodes ?? [];
            _totalEpisodes = totalEpisodes;
        }

        public int UpsertCount { get; private set; }

        public Task<WatchedEpisode?> GetByUserAndEpisodeAsync(
            Guid userId,
            Guid episodeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_existingEpisode);

        public Task<(WatchedEpisode Entity, bool Created)> UpsertAsync(
            WatchedEpisode watchedEpisode,
            CancellationToken cancellationToken = default)
        {
            UpsertCount++;
            return Task.FromResult((watchedEpisode, _upsertReturnsCreated));
        }

        public Task<bool> RemoveAsync(Guid userId, Guid episodeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<(IReadOnlyList<WatchedEpisode> Items, int TotalCount)> GetUserWatchedEpisodesAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var episode = CreateEpisode(EpisodeId1, 1, 1, "Pilot");
            var watchedEpisode = WatchedEpisode.Create(userId, episode.Id, DateTime.UtcNow);
            watchedEpisode.Episode = episode;
            return Task.FromResult<(IReadOnlyList<WatchedEpisode>, int)>(([watchedEpisode], _totalEpisodes));
        }

        public Task<int> CountWatchedForTvShowAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_watchedForTvShow);

        public Task<int> CountWatchedForSeasonAsync(
            Guid userId,
            Guid seasonId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_watchedForSeason);

        public Task<IReadOnlyList<SeasonEpisodeCountResult>> GetWatchedEpisodeCountsBySeasonAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default)
        {
            if (_watchedForSeason > 0)
            {
                return Task.FromResult<IReadOnlyList<SeasonEpisodeCountResult>>(
                    [new SeasonEpisodeCountResult(1, _watchedForSeason)]);
            }

            if (_watchedForTvShow > 0)
            {
                return Task.FromResult<IReadOnlyList<SeasonEpisodeCountResult>>(
                    [new SeasonEpisodeCountResult(1, _watchedForTvShow)]);
            }

            return Task.FromResult<IReadOnlyList<SeasonEpisodeCountResult>>([]);
        }

        public Task<IReadOnlyList<(
            Guid EpisodeId,
            Guid TvShowId,
            Guid SeasonId,
            string TvShowTitle,
            int SeasonNumber,
            int EpisodeNumber,
            string? EpisodeTitle,
            DateTime WatchedAt)>> GetRecentForUserAsync(
            Guid userId,
            int take,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_recentEpisodes);

        public Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_totalEpisodes);

        public Task<IReadOnlyList<(
            Guid TvShowId,
            string Title,
            string? OriginalTitle,
            string? PosterUrl,
            string? BackdropUrl,
            DateOnly? FirstAirDate,
            decimal VoteAverage,
            int VoteCount,
            DateTime LastWatchedAt)>> GetContinueWatchingTvShowsAsync(
            Guid userId,
            int take,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<(
                Guid TvShowId,
                string Title,
                string? OriginalTitle,
                string? PosterUrl,
                string? BackdropUrl,
                DateOnly? FirstAirDate,
                decimal VoteAverage,
                int VoteCount,
                DateTime LastWatchedAt)>>([]);

        public Task<IReadOnlyList<Guid>> GetWatchedEpisodeIdsForSeasonAsync(
            Guid userId,
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public int BulkMarkCount { get; private set; }

        public int BulkUnmarkCount { get; private set; }

        public Task<int> BulkMarkWatchedAsync(
            Guid userId,
            IReadOnlyList<Guid> episodeIds,
            DateTime watchedAt,
            CancellationToken cancellationToken = default)
        {
            BulkMarkCount++;
            return Task.FromResult(episodeIds.Distinct().Count());
        }

        public Task<int> BulkUnmarkWatchedAsync(
            Guid userId,
            IReadOnlyList<Guid> episodeIds,
            CancellationToken cancellationToken = default)
        {
            BulkUnmarkCount++;
            return Task.FromResult(episodeIds.Distinct().Count());
        }
    }

    private sealed class FakeMovieRepository(Movie? movie) : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(movie);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie> UpsertFromProviderAsync(MovieProviderDetails details, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeEpisodeRepository(
        Episode? episode,
        int totalEpisodes,
        Episode? nextEpisode,
        int totalSeasonEpisodes,
        Episode? nextSeasonEpisode) : IEpisodeRepository
    {
        public Task<Episode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(episode);

        public Task<int> CountByTvShowIdAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult(totalEpisodes);

        public Task<int> CountByTvShowIdAndSeasonNumberAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(totalSeasonEpisodes);

        public Task<IReadOnlyList<SeasonEpisodeCountResult>> GetEpisodeCountsBySeasonAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default)
        {
            if (totalEpisodes <= 0)
            {
                return Task.FromResult<IReadOnlyList<SeasonEpisodeCountResult>>([]);
            }

            var seasonTotal = Math.Max(totalEpisodes, totalSeasonEpisodes);
            if (seasonTotal <= 0)
            {
                return Task.FromResult<IReadOnlyList<SeasonEpisodeCountResult>>([]);
            }

            return Task.FromResult<IReadOnlyList<SeasonEpisodeCountResult>>(
                [new SeasonEpisodeCountResult(1, seasonTotal)]);
        }

        public Task<Episode?> GetFirstUnwatchedForTvShowAsync(
            Guid tvShowId,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(nextEpisode);

        public Task<Episode?> GetFirstUnwatchedForSeasonAsync(
            Guid tvShowId,
            int seasonNumber,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(nextSeasonEpisode);

        public Task<Episode?> GetBySeasonIdAndEpisodeNumberAsync(
            Guid seasonId,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Episode?>(null);

        public Task<Episode> UpsertFromProviderAsync(
            Guid seasonId,
            EpisodeProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> GetEpisodeIdsBelongingToTvShowAsync(
            Guid tvShowId,
            IReadOnlyList<Guid> episodeIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(episodeIds.Distinct().ToList());

        public Task<IReadOnlyList<Guid>> GetEpisodeIdsForTvShowUpToEpisodeAsync(
            Guid tvShowId,
            Guid targetEpisodeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(episode?.Id == targetEpisodeId ? [targetEpisodeId] : []);

        public Task<IReadOnlyList<Guid>> GetEpisodeIdsForSeasonAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(episode is null ? [] : [episode.Id]);

        public Task<IReadOnlyList<Guid>> GetEpisodeIdsForTvShowAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(episode is null ? [] : [episode.Id]);

        public Task<IReadOnlyList<Guid>> GetEpisodeIdsForRegularSeasonsAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(episode is null ? [] : [episode.Id]);
    }

    private sealed class FakeTvShowRepository(TvShow tvShow) : ITvShowRepository
    {
        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(tvShow);

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(null);

        public Task<TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSeasonRepository(Season? season) : ISeasonRepository
    {
        public Task<Season?> GetByTvShowIdAndSeasonNumberAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(season);

        public Task<Season> UpsertFromProviderAsync(
            Guid tvShowId,
            SeasonProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpsertSeasonsFromProviderAsync(
            Guid tvShowId,
            IReadOnlyList<SeasonProviderDetails> details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Season> UpsertSummaryFromProviderAsync(
            Guid tvShowId,
            SeasonProviderSummary summary,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlySet<int>> GetRegularSeasonNumbersWithEpisodesAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());
    }

    private sealed class FakeSeasonSummaryHydrator : ITvShowSeasonSummaryHydrator
    {
        public Task<TvShowSeasonSummaryHydrationResult> EnsureSeasonSummariesAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TvShowSeasonSummaryHydrationResult(CreateTvShow(), ProviderCatalogRefreshed: false));
    }

    private sealed class FakeCatalogSyncStateService : ITvShowCatalogSyncStateService
    {
        public Task MarkRefreshedAsync(
            Guid tvShowId,
            TvShowCatalogRefreshReason reason,
            DateTime refreshedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkChangeSignalAsync(
            Guid tvShowId,
            DateOnly changeSignalDate,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkChangesSyncAsync(
            Guid tvShowId,
            DateTime refreshedAtUtc,
            DateOnly changeSignalDate,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkHotReleaseAsync(
            Guid tvShowId,
            DateTime refreshedAtUtc,
            DateTime? nextHotCheckAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateNextHotCheckAsync(
            Guid tvShowId,
            DateTime? nextHotCheckAtUtc,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeGetSeasonService : IGetSeasonService
    {
        public Task<SeasonResult> GetSeasonAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SeasonResult(
                Guid.NewGuid(),
                tvShowId,
                seasonNumber,
                $"Season {seasonNumber}",
                null,
                null,
                0,
                null,
                []));
    }
}
