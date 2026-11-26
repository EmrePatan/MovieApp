using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Preferences;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Preferences;

public sealed class WatchPlatformPreferencesService(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser) : IWatchPlatformPreferencesService
{
    public async Task<WatchPlatformPreferencesResult> GetAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var preference = await dbContext.UserWatchPlatformPreferences.AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return new WatchPlatformPreferencesResult(preference?.ProviderIds ?? []);
    }

    public async Task<WatchPlatformPreferencesResult> UpdateAsync(
        IReadOnlyList<int> providerIds, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();

        // Same limits as the discover filter, so a saved selection can always be used there.
        var validation = AdvancedDiscoverValidator.ValidateWatchProviderIds(providerIds);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage!);
        }

        var preference = await dbContext.UserWatchPlatformPreferences
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (preference is null)
        {
            preference = new UserWatchPlatformPreference { UserId = userId };
            dbContext.UserWatchPlatformPreferences.Add(preference);
        }

        preference.ProviderIds = providerIds.Order().ToList();
        preference.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new WatchPlatformPreferencesResult(preference.ProviderIds);
    }

    private Guid GetUserId()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            throw new AuthenticationException("Authentication is required.");
        }

        return userId;
    }
}
