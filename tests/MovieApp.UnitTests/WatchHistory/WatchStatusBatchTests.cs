using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.WatchHistory;
using MovieApp.Application.Services.WatchHistory;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.WatchHistory;

public sealed class WatchStatusBatchTests
{
    [Fact]
    public async Task Service_ReturnsEveryRequestedIdIncludingUnwatchedOnes()
    {
        var userId = Guid.NewGuid();
        var watchedMovie = Guid.NewGuid();
        var unwatchedMovie = Guid.NewGuid();
        var caughtUpShow = Guid.NewGuid();
        var partialShow = Guid.NewGuid();
        var unknownShow = Guid.NewGuid();
        var watchedAt = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

        var repository = new FakeBatchRepository(
            [new WatchedMovieStatusRow(watchedMovie, watchedAt)],
            [
                new TvShowRegularProgressRow(caughtUpShow, 10, 10),
                new TvShowRegularProgressRow(partialShow, 10, 4),
            ]);
        var service = new WatchStatusBatchService(new FakeCurrentUser(userId), repository);

        var result = await service.GetAsync(
            [watchedMovie, unwatchedMovie, watchedMovie],
            [caughtUpShow, partialShow, unknownShow]);

        Assert.Equal(2, result.Movies.Count);
        Assert.Contains(result.Movies, movie => movie.MovieId == watchedMovie && movie.IsWatched && movie.WatchedAt == watchedAt);
        Assert.Contains(result.Movies, movie => movie.MovieId == unwatchedMovie && !movie.IsWatched && movie.WatchedAt is null);

        Assert.Equal(3, result.TvShows.Count);
        Assert.True(result.TvShows.Single(show => show.TvShowId == caughtUpShow).IsFullyWatched);
        Assert.False(result.TvShows.Single(show => show.TvShowId == partialShow).IsFullyWatched);
        var unknown = result.TvShows.Single(show => show.TvShowId == unknownShow);
        Assert.False(unknown.IsFullyWatched);
        Assert.Equal(0, unknown.RegularTotalEpisodes);

        Assert.Equal(userId, repository.LastUserId);
        Assert.Equal(2, repository.LastMovieIds!.Count);
    }

    [Fact]
    public async Task Service_SkipsRepositoryCallsForEmptyRequests()
    {
        var repository = new FakeBatchRepository([], []);
        var service = new WatchStatusBatchService(new FakeCurrentUser(Guid.NewGuid()), repository);

        var result = await service.GetAsync(null, []);

        Assert.Empty(result.Movies);
        Assert.Empty(result.TvShows);
        Assert.Equal(0, repository.MovieCalls);
        Assert.Equal(0, repository.TvCalls);
    }

    [Fact]
    public async Task Service_RejectsMoreThanTheMaximumIdsPerKind()
    {
        var service = new WatchStatusBatchService(
            new FakeCurrentUser(Guid.NewGuid()),
            new FakeBatchRepository([], []));
        var tooMany = Enumerable.Range(0, WatchStatusBatchService.MaxIdsPerKind + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();

        await Assert.ThrowsAsync<ValidationException>(() => service.GetAsync(tooMany, null));
        await Assert.ThrowsAsync<ValidationException>(() => service.GetAsync(null, tooMany));
    }

    [Fact]
    public async Task Service_RequiresAnAuthenticatedUser()
    {
        var service = new WatchStatusBatchService(
            new FakeCurrentUser(null),
            new FakeBatchRepository([], []));

        await Assert.ThrowsAsync<AuthenticationException>(() => service.GetAsync([Guid.NewGuid()], null));
    }

    [Fact]
    public async Task Repository_MatchesSingleShowProgressRules()
    {
        var today = EpisodeWatchEligibility.TodayUtc();
        await using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        // Show A: specials (season 0) are ignored, season 1 has a future episode that must not count,
        // season 2 has no ingested episodes so the summary count applies, season 3 has not aired yet.
        var showA = await SeedShowAsync(context, [
            new SeasonSeed(0, null, null, [today.AddDays(-30)]),
            new SeasonSeed(1, null, 3, [today.AddDays(-20), today.AddDays(-19), today.AddDays(-18), today.AddDays(5)]),
            new SeasonSeed(2, today.AddDays(-10), 8, []),
            new SeasonSeed(3, today.AddDays(30), 6, []),
        ]);
        var showB = await SeedShowAsync(context, [
            new SeasonSeed(1, null, 2, [today.AddDays(-2), today.AddDays(-1)]),
        ]);
        var showC = await SeedShowAsync(context, [
            new SeasonSeed(1, null, null, [today.AddDays(-2)]),
        ]);

        // User watched: A special + 2 of 3 aired season-1 episodes + the future one (ineligible);
        // B fully; C nothing. Another user's history must not leak in.
        await WatchAsync(context, userId, showA.EpisodeIdsBySeason[0][0]);
        await WatchAsync(context, userId, showA.EpisodeIdsBySeason[1][0]);
        await WatchAsync(context, userId, showA.EpisodeIdsBySeason[1][1]);
        await WatchAsync(context, userId, showA.EpisodeIdsBySeason[1][3]);
        foreach (var episodeId in showB.EpisodeIdsBySeason[1])
        {
            await WatchAsync(context, userId, episodeId);
        }

        await WatchAsync(context, otherUserId, showC.EpisodeIdsBySeason[1][0]);

        var batch = new WatchStatusBatchReadRepository(context);
        var rows = await batch.GetTvShowRegularProgressAsync(userId, [showA.TvShowId, showB.TvShowId, showC.TvShowId]);

        var episodeRepository = new EpisodeRepository(context);
        var watchedRepository = new WatchedEpisodeRepository(context, NullLogger<WatchedEpisodeRepository>.Instance);

        foreach (var show in new[] { showA, showB, showC })
        {
            var counts = await episodeRepository.GetEpisodeCountsBySeasonAsync(show.TvShowId);
            var watched = (await watchedRepository.GetWatchedEpisodeCountsBySeasonAsync(userId, show.TvShowId))
                .ToDictionary(item => item.SeasonNumber, item => item.EpisodeCount);
            var regular = counts.Where(season => season.SeasonNumber >= 1).ToList();
            var expectedTotal = regular.Sum(season => season.EpisodeCount);
            var expectedWatched = regular.Sum(season => watched.GetValueOrDefault(season.SeasonNumber));

            var row = rows.SingleOrDefault(item => item.TvShowId == show.TvShowId);
            Assert.NotNull(row);
            Assert.Equal(expectedTotal, row.RegularTotalEpisodes);
            Assert.Equal(expectedWatched, row.RegularWatchedEpisodes);
        }

        // Concrete numbers so a regression in both code paths cannot hide.
        var rowA = rows.Single(item => item.TvShowId == showA.TvShowId);
        Assert.Equal(3 + 8, rowA.RegularTotalEpisodes);
        Assert.Equal(2, rowA.RegularWatchedEpisodes);
        var rowB = rows.Single(item => item.TvShowId == showB.TvShowId);
        Assert.Equal(2, rowB.RegularTotalEpisodes);
        Assert.Equal(2, rowB.RegularWatchedEpisodes);
        Assert.Equal(0, rows.Single(item => item.TvShowId == showC.TvShowId).RegularWatchedEpisodes);
    }

    [Fact]
    public async Task Repository_ReturnsOnlyTheCurrentUsersWatchedMovies()
    {
        await using var context = CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var movieA = Guid.NewGuid();
        var movieB = Guid.NewGuid();
        var movieC = Guid.NewGuid();
        var watchedAt = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

        context.WatchedMovies.Add(WatchedMovie.Create(userId, movieA, watchedAt));
        context.WatchedMovies.Add(WatchedMovie.Create(otherUserId, movieB, watchedAt));
        context.WatchedMovies.Add(WatchedMovie.Create(userId, movieC, watchedAt));
        await context.SaveChangesAsync();

        var rows = await new WatchStatusBatchReadRepository(context)
            .GetWatchedMoviesAsync(userId, [movieA, movieB]);

        var row = Assert.Single(rows);
        Assert.Equal(movieA, row.MovieId);
        Assert.Equal(watchedAt, row.WatchedAt);
    }

    private sealed record SeasonSeed(int SeasonNumber, DateOnly? AirDate, int? SummaryEpisodeCount, DateOnly[] EpisodeAirDates);

    private sealed record SeededShow(Guid TvShowId, IReadOnlyDictionary<int, List<Guid>> EpisodeIdsBySeason);

    private static ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"watch-status-batch-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<SeededShow> SeedShowAsync(ApplicationDbContext context, SeasonSeed[] seasons)
    {
        var utcNow = DateTime.UtcNow;
        var tvShowId = Guid.NewGuid();
        var episodeIdsBySeason = new Dictionary<int, List<Guid>>();

        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = Random.Shared.Next(1_000_000, 9_999_999),
            Title = "Batch Status Show",
            Status = TvShowStatus.ReturningSeries,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

        foreach (var seed in seasons)
        {
            var seasonId = Guid.NewGuid();
            context.Seasons.Add(new Season
            {
                Id = seasonId,
                TvShowId = tvShowId,
                SeasonNumber = seed.SeasonNumber,
                AirDate = seed.AirDate,
                EpisodeCount = seed.SummaryEpisodeCount,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });

            var ids = new List<Guid>();
            for (var index = 0; index < seed.EpisodeAirDates.Length; index++)
            {
                var episodeId = Guid.NewGuid();
                ids.Add(episodeId);
                context.Episodes.Add(new Episode
                {
                    Id = episodeId,
                    SeasonId = seasonId,
                    EpisodeNumber = index + 1,
                    Name = $"Episode {index + 1}",
                    AirDate = seed.EpisodeAirDates[index],
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow,
                });
            }

            episodeIdsBySeason[seed.SeasonNumber] = ids;
        }

        await context.SaveChangesAsync();
        return new SeededShow(tvShowId, episodeIdsBySeason);
    }

    private static async Task WatchAsync(ApplicationDbContext context, Guid userId, Guid episodeId)
    {
        context.WatchedEpisodes.Add(WatchedEpisode.Create(userId, episodeId, DateTime.UtcNow));
        await context.SaveChangesAsync();
    }

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId is not null;

        public Guid? UserId => userId;
    }

    private sealed class FakeBatchRepository(
        IReadOnlyList<WatchedMovieStatusRow> movies,
        IReadOnlyList<TvShowRegularProgressRow> shows) : IWatchStatusBatchReadRepository
    {
        public Guid? LastUserId { get; private set; }

        public IReadOnlyCollection<Guid>? LastMovieIds { get; private set; }

        public int MovieCalls { get; private set; }

        public int TvCalls { get; private set; }

        public Task<IReadOnlyList<WatchedMovieStatusRow>> GetWatchedMoviesAsync(
            Guid userId,
            IReadOnlyCollection<Guid> movieIds,
            CancellationToken cancellationToken = default)
        {
            MovieCalls++;
            LastUserId = userId;
            LastMovieIds = movieIds;
            return Task.FromResult(movies);
        }

        public Task<IReadOnlyList<TvShowRegularProgressRow>> GetTvShowRegularProgressAsync(
            Guid userId,
            IReadOnlyCollection<Guid> tvShowIds,
            CancellationToken cancellationToken = default)
        {
            TvCalls++;
            return Task.FromResult(shows);
        }
    }
}
