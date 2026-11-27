using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.MovieChanges;

public sealed class TmdbChangesFailedTargetRepositoryTests
{
    private const int MaxRetries = 2;
    private static readonly DateTime Now = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Day = new(2026, 9, 14);

    [Fact]
    public async Task OnlyANewerChangeGivesAnExhaustedTitleItsRetriesBack()
    {
        await using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"tmdb-failed-targets-{Guid.NewGuid()}")
                .Options);
        var repository = new TmdbTvChangesSyncCheckpointRepository(context);
        var id = Guid.NewGuid();

        await repository.RecordFailuresAsync("tv", [id], Day, MaxRetries, Now);
        await repository.RecordRetryFailureAsync("tv", id, Now);
        await repository.RecordRetryFailureAsync("tv", id, Now);
        await repository.RecordFailuresAsync("tv", [id], Day, MaxRetries, Now);

        Assert.Empty(await repository.GetRetryableFailuresAsync("tv", MaxRetries, 10));
        Assert.Equal(1, await repository.CountExhaustedFailuresAsync("tv", MaxRetries));

        await repository.RecordFailuresAsync("tv", [id], Day.AddDays(1), MaxRetries, Now);

        var row = Assert.Single(await repository.GetRetryableFailuresAsync("tv", MaxRetries, 10));
        Assert.Equal(0, row.RetryCount);
        Assert.Empty(await repository.GetRetryableFailuresAsync("movie", MaxRetries, 10));

        await repository.ResolveFailuresAsync("tv", [id]);

        Assert.Empty(await repository.GetRetryableFailuresAsync("tv", MaxRetries, 10));
    }
}
