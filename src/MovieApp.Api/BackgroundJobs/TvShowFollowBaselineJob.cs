using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.TvShowFollows;

namespace MovieApp.Api.BackgroundJobs;

public sealed class TvShowFollowBaselineJob(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<TvShowFollowBaselineJob> logger)
{
    public const string QueueName = "tv-follow-baseline";

    [Queue(QueueName)]
    [AutomaticRetry(Attempts = 3)]
    public Task ExecuteAsync(Guid userId, Guid tvShowId) =>
        BackgroundJobOperationalRunner.RunAsync(
            logger,
            $"tv-show-follow-baseline:{userId:N}:{tvShowId:N}",
            () => ExecuteInternalAsync(userId, tvShowId));

    private async Task ExecuteInternalAsync(Guid userId, Guid tvShowId)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var followRepository = scope.ServiceProvider.GetRequiredService<ITvShowFollowRepository>();
        var baselineService = scope.ServiceProvider.GetRequiredService<ITvShowFollowBaselineService>();

        var follow = await followRepository.GetForUserAndTvShowForUpdateAsync(
            userId,
            tvShowId,
            CancellationToken.None);

        if (follow is null || follow.IsBaselineEstablished)
        {
            return;
        }

        try
        {
            await baselineService.EstablishAsync(follow, CancellationToken.None);
        }
        catch (NotFoundException)
        {
        }
    }
}
