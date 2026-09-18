using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Services.Insights;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Insights;

public sealed class InsightsV3RecordsSqlIntegrationTests
{
    [Fact]
    public async Task GetRecordsAsyncCalculatesStreakAndWeeklyPeaksInIstanbulTimeZone()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var userId = Guid.NewGuid();
        var tmdbIdBase = Random.Shared.Next(10_000_000, 90_000_000);
        var utcNow = DateTime.UtcNow;
        context.Users.Add(new User
        {
            Id = userId,
            Email = $"records-{userId:N}@example.com",
            NormalizedEmail = $"records-{userId:N}@example.com".ToUpperInvariant(),
            UserName = $"records-{userId:N}",
            DisplayName = "Records User",
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

        var movieIds = new[]
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
        };
        for (var index = 0; index < movieIds.Length; index++)
        {
            context.Movies.Add(new Movie
            {
                Id = movieIds[index],
                TmdbId = tmdbIdBase + index,
                Title = $"Record Movie {index + 1}",
                RuntimeMinutes = 100,
                VoteAverage = 7,
                VoteCount = 1,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
        }

        var tvShowId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = tvShowId,
            TmdbId = tmdbIdBase + 100,
            Title = "Record Show",
            Status = TvShowStatus.Ended,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = tvShowId,
            SeasonNumber = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.Episodes.Add(new Episode
        {
            Id = episodeId,
            SeasonId = seasonId,
            EpisodeNumber = 1,
            Name = "Pilot",
            RuntimeMinutes = 45,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

        context.WatchedMovies.AddRange(
            new WatchedMovie
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MovieId = movieIds[0],
                WatchedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            },
            new WatchedMovie
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MovieId = movieIds[1],
                WatchedAt = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc),
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            },
            new WatchedMovie
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MovieId = movieIds[2],
                WatchedAt = new DateTime(2026, 1, 3, 12, 0, 0, DateTimeKind.Utc),
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });

        context.WatchedEpisodes.Add(new WatchedEpisode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EpisodeId = episodeId,
            WatchedAt = new DateTime(2026, 1, 4, 12, 0, 0, DateTimeKind.Utc),
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

        await context.SaveChangesAsync();

        var records = await InsightsV3SqlQueries.GetRecordsAsync(
            context,
            userId,
            "Europe/Istanbul",
            CancellationToken.None);

        Assert.Equal(4, records.LongestStreakDays);
        Assert.Equal(3, records.BestMovieWeek?.Count);
        Assert.Equal(1, records.BestEpisodeWeek?.Count);
    }

    [Fact]
    public async Task GetV3RawDataAsyncCountsRepositoryPhasesAndPgCommandsSeparately()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;
        context.Users.Add(new User
        {
            Id = userId,
            Email = $"metrics-{userId:N}@example.com",
            NormalizedEmail = $"metrics-{userId:N}@example.com".ToUpperInvariant(),
            UserName = $"metrics-{userId:N}",
            DisplayName = "Metrics User",
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();

        var repository = new InsightsRepository(context);
        var timeZone = InsightsTimeZoneGuard.RequireValidTimeZone("Europe/Istanbul");
        var (_, metrics) = await repository.GetV3RawDataAsync(userId, timeZone, 2026);

        Assert.Equal(14, metrics.DbRoundTrips);
        Assert.Equal(17, metrics.PgCommandRoundTrips);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(IntegrationTestDatabase.GetConnectionString("movieapp_insights_v3_records_sql_tests"))
            .Options;

        return new ApplicationDbContext(options);
    }
}
