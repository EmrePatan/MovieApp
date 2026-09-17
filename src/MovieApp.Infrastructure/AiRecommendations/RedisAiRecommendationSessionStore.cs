using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Infrastructure.AiRecommendations;

internal sealed class RedisAiRecommendationSessionStore(
    ICacheService cacheService,
    IOptions<AiRecommendationOptions> options) : IAiRecommendationSessionStore
{
    public async Task<AiRecommendationSessionState?> GetAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await cacheService.GetAsync<AiRecommendationSessionState>(
                AiRecommendationCacheKeys.Session(userId, sessionId),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new AiRecommendationInfrastructureUnavailableException(
                "AI recommendation session storage is temporarily unavailable.");
        }
    }

    public async Task SaveAsync(
        Guid userId,
        AiRecommendationSessionState session,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ttl = TimeSpan.FromHours(Math.Max(1, options.Value.SessionTtlHours));
            await cacheService.SetAsync(
                AiRecommendationCacheKeys.Session(userId, session.SessionId),
                session,
                ttl,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new AiRecommendationInfrastructureUnavailableException(
                "AI recommendation session storage is temporarily unavailable.");
        }
    }
}
