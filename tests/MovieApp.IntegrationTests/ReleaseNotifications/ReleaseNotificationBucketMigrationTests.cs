using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Notifications;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.ReleaseNotifications;

public sealed class ReleaseNotificationBucketMigrationTests
{
    private const string PreviousMigration = "20261126192352_AddCatalogDetailExtras";

    [Fact]
    public async Task MigrationMergesDuplicateNotificationsAndKeepsTheirEventLinks()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(IntegrationTestDatabase.GetConnectionString("movieapp_release_bucket_migration_tests"))
            .Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureDeletedAsync();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);

        try
        {
            var now = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
            var user = User.Create(Guid.NewGuid(), "dedupe@example.com", "hash", "Dedupe User", now);
            var show = new TvShow { Id = Guid.NewGuid(), Title = "Dedupe Show", CreatedAt = now, UpdatedAt = now };
            var events = Enumerable.Range(1, 2)
                .Select(episode => CatalogReleaseEventFactory.CreateEpisodeEvent(
                    show.Id,
                    1,
                    episode,
                    new DateOnly(2026, 9, 15),
                    CatalogReleaseEventSource.BoundaryDetection,
                    now))
                .ToList();

            UserReleaseNotification Notification(string window, int minute) => new()
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TvShowId = show.Id,
                NotificationType = UserReleaseNotificationType.NewEpisodes,
                Status = UserReleaseNotificationStatus.Pending,
                AggregationWindowKey = window,
                CreatedAtUtc = now.AddMinutes(minute)
            };

            // Three rows in one bucket (one of them without links) and one row in another bucket.
            var first = Notification("2026-09-15", 0);
            var second = Notification("2026-09-15", 1);
            var orphan = Notification("2026-09-15", 2);
            var otherBucket = Notification("2026-09-16", 3);

            context.Users.Add(user);
            context.TvShows.Add(show);
            context.CatalogReleaseEvents.AddRange(events);
            context.UserReleaseNotifications.AddRange(first, second, orphan, otherBucket);
            context.UserReleaseNotificationEvents.AddRange(
                new UserReleaseNotificationEvent
                {
                    UserReleaseNotificationId = first.Id,
                    CatalogReleaseEventId = events[0].Id,
                    UserId = user.Id
                },
                new UserReleaseNotificationEvent
                {
                    UserReleaseNotificationId = second.Id,
                    CatalogReleaseEventId = events[1].Id,
                    UserId = user.Id
                });
            await context.SaveChangesAsync();

            await migrator.MigrateAsync();
            context.ChangeTracker.Clear();

            Assert.Equal(
                new[] { first.Id, otherBucket.Id }.Order(),
                (await context.UserReleaseNotifications.Select(row => row.Id).ToListAsync()).Order());
            var links = await context.UserReleaseNotificationEvents.ToListAsync();
            Assert.Equal(2, links.Count);
            Assert.All(links, link => Assert.Equal(first.Id, link.UserReleaseNotificationId));
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }
}
