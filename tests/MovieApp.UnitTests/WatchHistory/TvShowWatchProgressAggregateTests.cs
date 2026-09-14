using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.WatchHistory;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.TvShows;
using MovieApp.Application.Services.WatchHistory;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.WatchHistory;

public sealed class TvShowWatchProgressAggregateTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TvShowId = Guid.NewGuid();

    [Fact]
    public async Task GetTvShowWatchProgressAsyncReturnsSeasonBreakdown()
    {
        var service = new WatchHistoryService(
            new FakeCurrentUser(UserId),
            new FakeWatchedMovieRepository(),
            new FakeWatchedEpisodeRepository(
                watchedCountsBySeason: new Dictionary<int, int> { [1] = 8, [2] = 12 }),
            new FakeMovieRepository(),
            new FakeEpisodeRepository(
                episodeCountsBySeason: new Dictionary<int, int> { [1] = 8, [2] = 13, [0] = 2 }),
            new FakeTvShowRepository(CreateTvShow()),
            new FakeSeasonRepository(),
            new FakeGetSeasonService(),
            new FakeSeasonSummaryHydrator(),
            new FakeProfileStatisticsCache());

        var result = await service.GetTvShowWatchProgressAsync(TvShowId);

        Assert.Equal(23, result.TotalEpisodes);
        Assert.Equal(20, result.WatchedEpisodes);
        Assert.Equal(21, result.RegularTotalEpisodes);
        Assert.Equal(20, result.RegularWatchedEpisodes);
        Assert.False(result.IsFullyWatched);
        Assert.Equal(3, result.Seasons.Count);
        Assert.Contains(result.Seasons, season => season.SeasonNumber == 0 && season.TotalEpisodes == 2);
        Assert.Contains(
            result.Seasons,
            season => season.SeasonNumber == 1 && season.WatchedEpisodes == 8 && season.ProgressPercentage == 100m);
        Assert.Contains(
            result.Seasons,
            season => season.SeasonNumber == 2 && season.WatchedEpisodes == 12 && season.ProgressPercentage == 92.31m);
    }

    [Fact]
    public async Task BulkUpdateTvShowWatchStateIngestsRegularSeasonsBeforeBulkMark()
    {
        var getSeasonService = new TrackingGetSeasonService();
        var tvShow = CreateTvShow();
        tvShow.Seasons =
        [
            new Season { Id = Guid.NewGuid(), TvShowId = TvShowId, SeasonNumber = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Season { Id = Guid.NewGuid(), TvShowId = TvShowId, SeasonNumber = 2, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
        ];
        var service = new WatchHistoryService(
            new FakeCurrentUser(UserId),
            new FakeWatchedMovieRepository(),
            new TrackingWatchedEpisodeRepository(),
            new FakeMovieRepository(),
            new TrackingEpisodeRepository(),
            new FakeTvShowRepository(tvShow),
            new FakeSeasonRepository(),
            getSeasonService,
            new FakeSeasonSummaryHydrator(tvShow),
            new FakeProfileStatisticsCache());

        await service.BulkUpdateTvShowWatchStateAsync(TvShowId, watched: true);

        Assert.Equal([1, 2], getSeasonService.IngestedSeasonNumbers);
    }

    [Fact]
    public async Task BulkUpdateTvShowWatchStateUsesRegularSeasonEpisodesOnly()
    {
        var episodeRepository = new TrackingEpisodeRepository();
        var watchedEpisodeRepository = new TrackingWatchedEpisodeRepository();
        var service = new WatchHistoryService(
            new FakeCurrentUser(UserId),
            new FakeWatchedMovieRepository(),
            watchedEpisodeRepository,
            new FakeMovieRepository(),
            episodeRepository,
            new FakeTvShowRepository(CreateTvShow()),
            new FakeSeasonRepository(),
            new FakeGetSeasonService(),
            new FakeSeasonSummaryHydrator(),
            new FakeProfileStatisticsCache());

        var result = await service.BulkUpdateTvShowWatchStateAsync(TvShowId, watched: true);

        Assert.Equal(2, result.AffectedCount);
        Assert.Equal([Guid.Parse("11111111-1111-1111-1111-111111111101"), Guid.Parse("11111111-1111-1111-1111-111111111102")], episodeRepository.RegularSeasonEpisodeIdsRequested);
        Assert.Equal(1, watchedEpisodeRepository.BulkMarkCalls);
    }

    [Fact]
    public async Task GetTvShowWatchProgressAsyncMarksFullyWatchedWhenOnlyRegularSeasonsComplete()
    {
        var service = new WatchHistoryService(
            new FakeCurrentUser(UserId),
            new FakeWatchedMovieRepository(),
            new FakeWatchedEpisodeRepository(
                watchedCountsBySeason: new Dictionary<int, int> { [1] = 8, [2] = 13 }),
            new FakeMovieRepository(),
            new FakeEpisodeRepository(
                episodeCountsBySeason: new Dictionary<int, int> { [1] = 8, [2] = 13, [0] = 2 }),
            new FakeTvShowRepository(CreateTvShow()),
            new FakeSeasonRepository(),
            new FakeGetSeasonService(),
            new FakeSeasonSummaryHydrator(),
            new FakeProfileStatisticsCache());

        var result = await service.GetTvShowWatchProgressAsync(TvShowId);

        Assert.True(result.IsFullyWatched);
        Assert.Equal(21, result.RegularTotalEpisodes);
        Assert.Equal(21, result.RegularWatchedEpisodes);
    }

    [Fact]
    public async Task GetTvShowWatchProgressAsyncUsesGroupedQueriesNotPerSeasonCounts()
    {
        var episodeRepository = new CountingEpisodeRepository();
        var watchedEpisodeRepository = new CountingWatchedEpisodeRepository();
        var service = new WatchHistoryService(
            new FakeCurrentUser(UserId),
            new FakeWatchedMovieRepository(),
            watchedEpisodeRepository,
            new FakeMovieRepository(),
            episodeRepository,
            new FakeTvShowRepository(CreateTvShow()),
            new FakeSeasonRepository(),
            new FakeGetSeasonService(),
            new FakeSeasonSummaryHydrator(),
            new FakeProfileStatisticsCache());

        await service.GetTvShowWatchProgressAsync(TvShowId);

        Assert.Equal(1, episodeRepository.EpisodeCountsBySeasonCalls);
        Assert.Equal(1, watchedEpisodeRepository.WatchedCountsBySeasonCalls);
        Assert.Equal(0, episodeRepository.CountByTvShowIdCalls);
        Assert.Equal(0, watchedEpisodeRepository.CountWatchedForTvShowCalls);
    }

    private static TvShow CreateTvShow() =>
        new()
        {
            Id = TvShowId,
            Title = "Breaking Bad",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class FakeProfileStatisticsCache : IProfileStatisticsCache
    {
        public Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<UserStatisticsResult?> GetAsync(
            Guid userId,
            string? timeZoneId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<UserStatisticsResult?>(null);

        public Task SetAsync(
            Guid userId,
            string? timeZoneId,
            UserStatisticsResult statistics,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeWatchedMovieRepository : IWatchedMovieRepository
    {
        public Task<WatchedMovie?> GetByUserAndMovieAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default) =>
            Task.FromResult<WatchedMovie?>(null);

        public Task<(WatchedMovie Entity, bool Created)> UpsertAsync(WatchedMovie watchedMovie, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RemoveAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<WatchedMovie> Items, int TotalCount)> GetUserWatchedMoviesAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<(Guid MovieId, string Title, DateTime WatchedAt)>> GetRecentForUserAsync(
            Guid userId,
            int take,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed class FakeMovieRepository : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie> UpsertFromProviderAsync(MovieProviderDetails details, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTvShowRepository(TvShow tvShow) : ITvShowRepository
    {
        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(tvShow);

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(null);

        public Task<TvShow> UpsertFromProviderAsync(TvShowProviderDetails details, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSeasonRepository : ISeasonRepository
    {
        public Task<Season?> GetByTvShowIdAndSeasonNumberAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Season?>(null);

        public Task<Season> UpsertFromProviderAsync(
            Guid tvShowId,
            SeasonProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Season> UpsertSummaryFromProviderAsync(
            Guid tvShowId,
            SeasonProviderSummary summary,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private class FakeWatchedEpisodeRepository(
        IReadOnlyDictionary<int, int>? watchedCountsBySeason = null) : IWatchedEpisodeRepository
    {
        private readonly IReadOnlyDictionary<int, int> _watchedCountsBySeason =
            watchedCountsBySeason ?? new Dictionary<int, int>();

        public Task<WatchedEpisode?> GetByUserAndEpisodeAsync(Guid userId, Guid episodeId, CancellationToken cancellationToken = default) =>
            Task.FromResult<WatchedEpisode?>(null);

        public Task<(WatchedEpisode Entity, bool Created)> UpsertAsync(WatchedEpisode watchedEpisode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RemoveAsync(Guid userId, Guid episodeId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<WatchedEpisode> Items, int TotalCount)> GetUserWatchedEpisodesAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> CountWatchedForTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_watchedCountsBySeason.Values.Sum());

        public Task<int> CountWatchedForSeasonAsync(Guid userId, Guid seasonId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyList<SeasonEpisodeCountResult>> GetWatchedEpisodeCountsBySeasonAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SeasonEpisodeCountResult>>(
                _watchedCountsBySeason
                    .Select(pair => new SeasonEpisodeCountResult(pair.Key, pair.Value))
                    .OrderBy(result => result.SeasonNumber)
                    .ToList());

        public Task<IReadOnlyList<(
            Guid EpisodeId,
            Guid TvShowId,
            Guid SeasonId,
            string TvShowTitle,
            int SeasonNumber,
            int EpisodeNumber,
            string? EpisodeTitle,
            DateTime WatchedAt)>> GetRecentForUserAsync(Guid userId, int take, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyList<(
            Guid TvShowId,
            string Title,
            string? OriginalTitle,
            string? PosterUrl,
            string? BackdropUrl,
            DateOnly? FirstAirDate,
            decimal VoteAverage,
            int VoteCount,
            DateTime LastWatchedAt)>> GetContinueWatchingTvShowsAsync(Guid userId, int take, CancellationToken cancellationToken = default) =>
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

        public virtual Task<int> BulkMarkWatchedAsync(
            Guid userId,
            IReadOnlyList<Guid> episodeIds,
            DateTime watchedAt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(episodeIds.Count);

        public virtual Task<int> BulkUnmarkWatchedAsync(
            Guid userId,
            IReadOnlyList<Guid> episodeIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(episodeIds.Count);
    }

    private sealed class CountingWatchedEpisodeRepository : IWatchedEpisodeRepository
    {
        public int WatchedCountsBySeasonCalls { get; private set; }

        public int CountWatchedForTvShowCalls { get; private set; }

        public Task<WatchedEpisode?> GetByUserAndEpisodeAsync(Guid userId, Guid episodeId, CancellationToken cancellationToken = default) =>
            Task.FromResult<WatchedEpisode?>(null);

        public Task<(WatchedEpisode Entity, bool Created)> UpsertAsync(WatchedEpisode watchedEpisode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> RemoveAsync(Guid userId, Guid episodeId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(IReadOnlyList<WatchedEpisode> Items, int TotalCount)> GetUserWatchedEpisodesAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> CountWatchedForTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default)
        {
            CountWatchedForTvShowCalls++;
            return Task.FromResult(0);
        }

        public Task<int> CountWatchedForSeasonAsync(Guid userId, Guid seasonId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyList<SeasonEpisodeCountResult>> GetWatchedEpisodeCountsBySeasonAsync(
            Guid userId,
            Guid tvShowId,
            CancellationToken cancellationToken = default)
        {
            WatchedCountsBySeasonCalls++;
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
            DateTime WatchedAt)>> GetRecentForUserAsync(Guid userId, int take, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> CountForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyList<(
            Guid TvShowId,
            string Title,
            string? OriginalTitle,
            string? PosterUrl,
            string? BackdropUrl,
            DateOnly? FirstAirDate,
            decimal VoteAverage,
            int VoteCount,
            DateTime LastWatchedAt)>> GetContinueWatchingTvShowsAsync(Guid userId, int take, CancellationToken cancellationToken = default) =>
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

        public Task<int> BulkMarkWatchedAsync(
            Guid userId,
            IReadOnlyList<Guid> episodeIds,
            DateTime watchedAt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<int> BulkUnmarkWatchedAsync(
            Guid userId,
            IReadOnlyList<Guid> episodeIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private class FakeEpisodeRepository(
        IReadOnlyDictionary<int, int>? episodeCountsBySeason = null) : IEpisodeRepository
    {
        private readonly IReadOnlyDictionary<int, int> _episodeCountsBySeason =
            episodeCountsBySeason ?? new Dictionary<int, int>();

        public virtual Task<Episode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Episode?>(null);

        public Task<int> CountByTvShowIdAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_episodeCountsBySeason.Values.Sum());

        public Task<int> CountByTvShowIdAndSeasonNumberAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_episodeCountsBySeason.GetValueOrDefault(seasonNumber));

        public Task<IReadOnlyList<SeasonEpisodeCountResult>> GetEpisodeCountsBySeasonAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SeasonEpisodeCountResult>>(
                _episodeCountsBySeason
                    .Select(pair => new SeasonEpisodeCountResult(pair.Key, pair.Value))
                    .OrderBy(result => result.SeasonNumber)
                    .ToList());

        public Task<Episode?> GetFirstUnwatchedForTvShowAsync(
            Guid tvShowId,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Episode?>(null);

        public Task<Episode?> GetFirstUnwatchedForSeasonAsync(
            Guid tvShowId,
            int seasonNumber,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Episode?>(null);

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
            Task.FromResult<IReadOnlyList<Guid>>(episodeIds);

        public Task<IReadOnlyList<Guid>> GetEpisodeIdsForTvShowUpToEpisodeAsync(
            Guid tvShowId,
            Guid targetEpisodeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<IReadOnlyList<Guid>> GetEpisodeIdsForSeasonAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<IReadOnlyList<Guid>> GetEpisodeIdsForTvShowAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public virtual Task<IReadOnlyList<Guid>> GetEpisodeIdsForRegularSeasonsAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class CountingEpisodeRepository : IEpisodeRepository
    {
        public int EpisodeCountsBySeasonCalls { get; private set; }

        public int CountByTvShowIdCalls { get; private set; }

        public Task<Episode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Episode?>(null);

        public Task<int> CountByTvShowIdAsync(Guid tvShowId, CancellationToken cancellationToken = default)
        {
            CountByTvShowIdCalls++;
            return Task.FromResult(0);
        }

        public Task<int> CountByTvShowIdAndSeasonNumberAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyList<SeasonEpisodeCountResult>> GetEpisodeCountsBySeasonAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default)
        {
            EpisodeCountsBySeasonCalls++;
            return Task.FromResult<IReadOnlyList<SeasonEpisodeCountResult>>([]);
        }

        public Task<Episode?> GetFirstUnwatchedForTvShowAsync(
            Guid tvShowId,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Episode?>(null);

        public Task<Episode?> GetFirstUnwatchedForSeasonAsync(
            Guid tvShowId,
            int seasonNumber,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Episode?>(null);

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
            Task.FromResult<IReadOnlyList<Guid>>(episodeIds);

        public Task<IReadOnlyList<Guid>> GetEpisodeIdsForTvShowUpToEpisodeAsync(
            Guid tvShowId,
            Guid targetEpisodeId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<IReadOnlyList<Guid>> GetEpisodeIdsForSeasonAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<IReadOnlyList<Guid>> GetEpisodeIdsForTvShowAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<IReadOnlyList<Guid>> GetEpisodeIdsForRegularSeasonsAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class TrackingEpisodeRepository : FakeEpisodeRepository
    {
        private static readonly Guid Season1EpisodeId = Guid.Parse("11111111-1111-1111-1111-111111111101");
        private static readonly Guid Season2EpisodeId = Guid.Parse("11111111-1111-1111-1111-111111111102");

        public IReadOnlyList<Guid> RegularSeasonEpisodeIdsRequested { get; private set; } = [];

        public override Task<IReadOnlyList<Guid>> GetEpisodeIdsForRegularSeasonsAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default)
        {
            RegularSeasonEpisodeIdsRequested = [Season1EpisodeId, Season2EpisodeId];
            return Task.FromResult<IReadOnlyList<Guid>>(RegularSeasonEpisodeIdsRequested);
        }
    }

    private sealed class TrackingWatchedEpisodeRepository : FakeWatchedEpisodeRepository
    {
        public int BulkMarkCalls { get; private set; }

        public override Task<int> BulkMarkWatchedAsync(
            Guid userId,
            IReadOnlyList<Guid> episodeIds,
            DateTime watchedAt,
            CancellationToken cancellationToken = default)
        {
            BulkMarkCalls++;
            return base.BulkMarkWatchedAsync(userId, episodeIds, watchedAt, cancellationToken);
        }
    }

    private sealed class FakeSeasonSummaryHydrator : ITvShowSeasonSummaryHydrator
    {
        private readonly TvShow _tvShow;

        public FakeSeasonSummaryHydrator(TvShow? tvShow = null) =>
            _tvShow = tvShow ?? CreateTvShow();

        public Task<TvShow> EnsureSeasonSummariesAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_tvShow);
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

    private sealed class TrackingGetSeasonService : IGetSeasonService
    {
        private readonly FakeGetSeasonService _inner = new();

        public List<int> IngestedSeasonNumbers { get; } = [];

        public async Task<SeasonResult> GetSeasonAsync(
            Guid tvShowId,
            int seasonNumber,
            CancellationToken cancellationToken = default)
        {
            IngestedSeasonNumbers.Add(seasonNumber);
            return await _inner.GetSeasonAsync(tvShowId, seasonNumber, cancellationToken);
        }
    }
}
