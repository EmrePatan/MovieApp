using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.PushNotifications;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.PushNotifications;
using MovieApp.Application.Services.PushNotifications;
using MovieApp.Application.Services.Social;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Social;

public sealed partial class SocialPushNotifier(
    ApplicationDbContext dbContext,
    IExpoPushClient expoPushClient,
    IOptions<PushNotificationsOptions> pushOptions,
    ILogger<SocialPushNotifier> logger) : ISocialPushNotifier
{
    public const string PushDataType = "social";

    private static readonly TimeSpan PushTimeout = TimeSpan.FromSeconds(8);

    public async Task NotifyAsync(
        IReadOnlyCollection<SocialNotification> notifications,
        CancellationToken cancellationToken = default)
    {
        if (notifications.Count == 0 || !pushOptions.Value.Enabled)
        {
            return;
        }

        foreach (var notification in notifications)
        {
            try
            {
                await NotifyOneAsync(notification, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException
                                              || !cancellationToken.IsCancellationRequested)
            {
                LogPushFailed(logger, exception, notification.Id);
            }
        }
    }

    private async Task NotifyOneAsync(SocialNotification notification, CancellationToken cancellationToken)
    {
        // Same switch as direct messages: one social/message push opt-out covers both.
        var optedOut = await dbContext.UserNotificationPreferences.AsNoTracking()
            .AnyAsync(
                preference => preference.UserId == notification.RecipientId && !preference.MessagePushEnabled,
                cancellationToken);
        if (optedOut)
        {
            return;
        }

        var devices = await dbContext.PushDevices
            .Where(device => device.UserId == notification.RecipientId
                && device.IsActive
                && device.ExpoPushToken != "")
            .ToListAsync(cancellationToken);
        if (devices.Count == 0)
        {
            return;
        }

        var actor = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == notification.ActorId)
            .Select(user => new { user.DisplayName, user.UserName })
            .FirstOrDefaultAsync(cancellationToken);
        var title = string.IsNullOrWhiteSpace(actor?.DisplayName)
            ? actor?.UserName ?? "Movie Cave"
            : actor.DisplayName;

        var data = new Dictionary<string, string>
        {
            ["type"] = PushDataType,
            ["notificationType"] = notification.NotificationType,
            ["notificationId"] = notification.Id.ToString(),
            ["actorId"] = notification.ActorId.ToString(),
        };
        if (notification.ActivityId.HasValue)
        {
            data["activityId"] = notification.ActivityId.Value.ToString();
        }

        if (notification.CommentId.HasValue)
        {
            data["commentId"] = notification.CommentId.Value.ToString();
        }

        var sendable = new List<(PushDevice Device, PushNotificationMessage Message)>();
        foreach (var device in devices)
        {
            var body = SocialPushText.Compose(notification.NotificationType, device.ContentLocale);
            if (body is null)
            {
                continue;
            }

            sendable.Add((device, new PushNotificationMessage(
                Guid.NewGuid(), device.ExpoPushToken, title, body, data)));
        }

        if (sendable.Count == 0)
        {
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(PushTimeout);
        var results = await expoPushClient.SendAsync(sendable.Select(item => item.Message).ToArray(), timeout.Token);

        var utcNow = DateTime.UtcNow;
        var deactivated = false;
        for (var index = 0; index < results.Count && index < sendable.Count; index++)
        {
            var result = results[index];
            if (!result.IsSuccess && ExpoPushErrorClassifier.IsPermanentDeviceError(result.ErrorCode))
            {
                sendable[index].Device.IsActive = false;
                sendable[index].Device.UpdatedAtUtc = utcNow;
                deactivated = true;
            }
        }

        if (deactivated)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    [LoggerMessage(
        EventId = 7411,
        Level = LogLevel.Warning,
        Message = "Social push failed for notification {NotificationId}")]
    private static partial void LogPushFailed(ILogger logger, Exception exception, Guid notificationId);
}
