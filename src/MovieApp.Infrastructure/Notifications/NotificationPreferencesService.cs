using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Notifications;
using MovieApp.Application.Exceptions;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Notifications;

public sealed class NotificationPreferencesService(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser) : INotificationPreferencesService
{
    public async Task<NotificationPreferencesResult> GetAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var preference = await dbContext.UserNotificationPreferences.AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return preference is null
            ? new NotificationPreferencesResult(true, true)
            : new NotificationPreferencesResult(preference.MessagePushEnabled, preference.ReleasePushEnabled);
    }

    public async Task<NotificationPreferencesResult> UpdateAsync(
        bool messagePushEnabled, bool releasePushEnabled, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var preference = await dbContext.UserNotificationPreferences
            .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (preference is null)
        {
            preference = new UserNotificationPreference { UserId = userId };
            dbContext.UserNotificationPreferences.Add(preference);
        }

        preference.MessagePushEnabled = messagePushEnabled;
        preference.ReleasePushEnabled = releasePushEnabled;
        preference.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new NotificationPreferencesResult(messagePushEnabled, releasePushEnabled);
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
