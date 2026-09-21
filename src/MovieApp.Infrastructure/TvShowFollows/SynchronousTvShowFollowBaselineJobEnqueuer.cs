using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.TvShowFollows;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.TvShowFollows;

namespace MovieApp.Infrastructure.TvShowFollows;

public sealed class SynchronousTvShowFollowBaselineJobEnqueuer(
    IServiceScopeFactory serviceScopeFactory) : ITvShowFollowBaselineJobEnqueuer
{
    public async Task EnqueueAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var followRepository = scope.ServiceProvider.GetRequiredService<ITvShowFollowRepository>();
        var baselineService = scope.ServiceProvider.GetRequiredService<ITvShowFollowBaselineService>();

        var follow = await followRepository.GetForUserAndTvShowForUpdateAsync(userId, tvShowId, cancellationToken);
        if (follow is null)
        {
            return;
        }

        await baselineService.EstablishAsync(follow, cancellationToken);
    }
}
