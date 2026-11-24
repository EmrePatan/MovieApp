using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.PushNotifications;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.PushNotifications;
using MovieApp.Application.Services.Social;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Social;

namespace MovieApp.UnitTests.Social;

public sealed class SocialPushNotifierTests
{
    [Theory]
    [InlineData(SocialPushText.ActivityLike)]
    [InlineData(SocialPushText.CommentLike)]
    [InlineData(SocialPushText.NewComment)]
    [InlineData(SocialPushText.CommentReply)]
    [InlineData(SocialPushText.NewFollower)]
    [InlineData(SocialPushText.FollowRequest)]
    [InlineData(SocialPushText.FollowAccepted)]
    public void Text_ExistsForEveryNotificationTypeAndLocale(string type)
    {
        foreach (var locale in new[] { "en-US", "tr-TR", "es-ES", "de-DE", "fr-FR", "it-IT", "pt-BR", null })
        {
            Assert.False(string.IsNullOrWhiteSpace(SocialPushText.Compose(type, locale)));
        }
    }

    [Fact]
    public void Text_UsesTurkishForTurkishDevicesAndEnglishOtherwise()
    {
        Assert.Equal("yorumuna yanıt verdi", SocialPushText.Compose(SocialPushText.CommentReply, "tr-TR"));
        Assert.Equal("replied to your comment", SocialPushText.Compose(SocialPushText.CommentReply, "xx-XX"));
        Assert.Null(SocialPushText.Compose("DirectMessage", "en-US"));
    }

    [Fact]
    public async Task Notify_SendsOnePushPerActiveDeviceWithRoutingData()
    {
        await using var context = CreateContext();
        var recipientId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        context.Users.Add(new User { Id = actorId, UserName = "ayse", DisplayName = "Ayşe" });
        context.PushDevices.AddRange(
            Device(recipientId, "ExponentPushToken[tr]", "tr-TR", isActive: true),
            Device(recipientId, "ExponentPushToken[en]", "en-US", isActive: true),
            Device(recipientId, "ExponentPushToken[off]", "en-US", isActive: false));
        await context.SaveChangesAsync();
        var client = new RecordingPushClient();
        var notifier = CreateNotifier(context, client);

        await notifier.NotifyAsync(
            [SocialNotification.Create(recipientId, actorId, "CommentReply", activityId, commentId, null, DateTime.UtcNow)]);

        Assert.Equal(2, client.Sent.Count);
        Assert.All(client.Sent, message => Assert.Equal("Ayşe", message.Title));
        Assert.Equal("yorumuna yanıt verdi", client.Sent.Single(message => message.ExpoPushToken.Contains("[tr]")).Body);
        Assert.Equal("replied to your comment", client.Sent.Single(message => message.ExpoPushToken.Contains("[en]")).Body);
        var data = client.Sent[0].Data;
        Assert.Equal("social", data["type"]);
        Assert.Equal("CommentReply", data["notificationType"]);
        Assert.Equal(activityId.ToString(), data["activityId"]);
        Assert.Equal(commentId.ToString(), data["commentId"]);
        Assert.Equal(actorId.ToString(), data["actorId"]);
    }

    [Fact]
    public async Task Notify_RespectsOptOutAndDisabledPush()
    {
        await using var context = CreateContext();
        var optedOutRecipient = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        context.Users.Add(new User { Id = actorId, UserName = "ayse", DisplayName = "Ayşe" });
        context.PushDevices.Add(Device(optedOutRecipient, "ExponentPushToken[a]", "en-US", isActive: true));
        context.UserNotificationPreferences.Add(new UserNotificationPreference
        {
            UserId = optedOutRecipient,
            MessagePushEnabled = false,
        });
        await context.SaveChangesAsync();

        var client = new RecordingPushClient();
        await CreateNotifier(context, client).NotifyAsync(
            [SocialNotification.Create(optedOutRecipient, actorId, "NewFollower", null, null, Guid.NewGuid(), DateTime.UtcNow)]);
        Assert.Empty(client.Sent);

        var disabledClient = new RecordingPushClient();
        await CreateNotifier(context, disabledClient, enabled: false).NotifyAsync(
            [SocialNotification.Create(Guid.NewGuid(), actorId, "NewFollower", null, null, Guid.NewGuid(), DateTime.UtcNow)]);
        Assert.Empty(disabledClient.Sent);
    }

    [Fact]
    public async Task Notify_DeactivatesDevicesExpoRejectsPermanently()
    {
        await using var context = CreateContext();
        var recipientId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        context.Users.Add(new User { Id = actorId, UserName = "ayse", DisplayName = "Ayşe" });
        var device = Device(recipientId, "ExponentPushToken[gone]", "en-US", isActive: true);
        context.PushDevices.Add(device);
        await context.SaveChangesAsync();
        var client = new RecordingPushClient { ErrorCode = "DeviceNotRegistered" };

        await CreateNotifier(context, client).NotifyAsync(
            [SocialNotification.Create(recipientId, actorId, "ActivityLike", Guid.NewGuid(), null, null, DateTime.UtcNow)]);

        Assert.False((await context.PushDevices.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task Notify_NeverThrowsWhenTheClientFails()
    {
        await using var context = CreateContext();
        var recipientId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        context.Users.Add(new User { Id = actorId, UserName = "ayse", DisplayName = "Ayşe" });
        context.PushDevices.Add(Device(recipientId, "ExponentPushToken[x]", "en-US", isActive: true));
        await context.SaveChangesAsync();

        var exception = await Record.ExceptionAsync(() => CreateNotifier(context, new RecordingPushClient { Throw = true })
            .NotifyAsync(
                [SocialNotification.Create(recipientId, actorId, "ActivityLike", Guid.NewGuid(), null, null, DateTime.UtcNow)]));

        Assert.Null(exception);
    }

    private static SocialPushNotifier CreateNotifier(
        ApplicationDbContext context, IExpoPushClient client, bool enabled = true) =>
        new(
            context,
            client,
            Options.Create(new PushNotificationsOptions { Enabled = enabled }),
            NullLogger<SocialPushNotifier>.Instance);

    private static PushDevice Device(Guid userId, string token, string locale, bool isActive) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        ExpoPushToken = token,
        Platform = PushDevicePlatform.Ios,
        ContentLocale = locale,
        IsActive = isActive,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow,
    };

    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"social-push-{Guid.NewGuid()}")
            .Options);

    private sealed class RecordingPushClient : IExpoPushClient
    {
        public List<PushNotificationMessage> Sent { get; } = [];

        public string? ErrorCode { get; init; }

        public bool Throw { get; init; }

        public Task<IReadOnlyList<ExpoPushSendResult>> SendAsync(
            IReadOnlyList<PushNotificationMessage> messages,
            CancellationToken cancellationToken = default)
        {
            if (Throw)
            {
                throw new HttpRequestException("expo unreachable");
            }

            Sent.AddRange(messages);
            IReadOnlyList<ExpoPushSendResult> results = messages
                .Select(message => new ExpoPushSendResult(
                    message.DeliveryId,
                    ErrorCode is null,
                    ErrorCode is null ? "ticket" : null,
                    ErrorCode,
                    null,
                    ErrorCode is not null,
                    false))
                .ToList();
            return Task.FromResult(results);
        }

        public Task<IReadOnlyList<ExpoPushReceiptResult>> GetReceiptsAsync(
            IReadOnlyCollection<string> ticketIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ExpoPushReceiptResult>>([]);
    }
}
