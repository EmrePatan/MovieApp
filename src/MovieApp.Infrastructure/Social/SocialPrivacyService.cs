using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Exceptions;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Social;

public sealed class SocialPrivacyService(ApplicationDbContext dbContext, ICurrentUser currentUser)
    : ISocialPrivacyService
{
    public async Task<SocialPrivacyResult> GetAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var settings = await GetOrCreateAsync(userId, cancellationToken);
        return ToResult(settings);
    }

    public async Task<SocialPrivacyResult> UpdateAsync(
        SocialPrivacyResult settings, CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var entity = await GetOrCreateAsync(userId, cancellationToken);
        entity.Update(settings.IsPrivate, settings.ShowWatched, settings.ShowRatings,
            settings.ShowFavorites, settings.ShowReviews, DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResult(entity);
    }

    private async Task<UserPrivacySettings> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var settings = await dbContext.UserPrivacySettings.FirstOrDefaultAsync(
            item => item.UserId == userId, cancellationToken);
        if (settings is not null) return settings;

        settings = UserPrivacySettings.CreateDefault(userId, DateTime.UtcNow);
        dbContext.UserPrivacySettings.Add(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private Guid RequireUserId()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty)
            throw new AuthenticationException("Authentication is required.");
        return userId;
    }

    private static SocialPrivacyResult ToResult(UserPrivacySettings settings) =>
        new(settings.IsPrivate, settings.ShowWatched, settings.ShowRatings, settings.ShowFavorites, settings.ShowReviews);
}
