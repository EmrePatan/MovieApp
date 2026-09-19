using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Infrastructure.AiRecommendations;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class AiRecommendationQuotaTests
{
    [Fact]
    public async Task InMemoryQuotaEnforcesDailyUserLimit()
    {
        var service = CreateService(new AiRecommendationOptions { UserDailyMessageLimit = 2 });
        var userId = Guid.NewGuid();

        var first = await service.CheckAndReserveAsync(userId);
        await service.CommitAsync(userId, first);
        var second = await service.CheckAndReserveAsync(userId);
        await service.CommitAsync(userId, second);

        await Assert.ThrowsAsync<AiRecommendationQuotaExceededException>(() =>
            service.CheckAndReserveAsync(userId));
    }

    [Fact]
    public async Task InMemoryQuotaReleasesOnProviderFailure()
    {
        var service = CreateService(new AiRecommendationOptions { UserDailyMessageLimit = 1 });
        var userId = Guid.NewGuid();

        var reservation = await service.CheckAndReserveAsync(userId);
        await service.ReleaseAsync(userId, reservation);

        var retry = await service.CheckAndReserveAsync(userId);
        Assert.NotNull(retry);
    }

    [Fact]
    public async Task InMemoryQuotaReleaseRestoresCapacityWithoutCommit()
    {
        var service = CreateService(new AiRecommendationOptions { UserDailyMessageLimit = 1 });
        var userId = Guid.NewGuid();

        var reservation = await service.CheckAndReserveAsync(userId);
        await service.ReleaseAsync(userId, reservation);

        var retry = await service.CheckAndReserveAsync(userId);
        Assert.NotNull(retry);

        var remaining = await service.GetRemainingUserQuotaAsync(userId);
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task InMemoryQuotaSupportsConcurrentReservationsSafely()
    {
        var service = CreateService(new AiRecommendationOptions
        {
            UserDailyMessageLimit = 1,
            GlobalDailyRequestCap = 100,
            RpmLimit = 100
        });
        var userId = Guid.NewGuid();

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(async () =>
            {
                try
                {
                    var reservation = await service.CheckAndReserveAsync(userId);
                    await service.CommitAsync(userId, reservation);
                    return true;
                }
                catch (AiRecommendationQuotaExceededException)
                {
                    return false;
                }
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(success => success));
        Assert.Equal(9, results.Count(success => !success));
    }

    private static AiRecommendationQuotaService CreateService(AiRecommendationOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<AiRecommendationOptions>>(Options.Create(options));
        services.AddSingleton<IOptions<RedisOptions>>(Options.Create(new RedisOptions()));
        services.AddLogging();
        services.AddSingleton<AiRecommendationQuotaService>();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<AiRecommendationQuotaService>();
    }
}
