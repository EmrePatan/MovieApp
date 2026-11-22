using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Messages;
using MovieApp.Application.Abstractions.PushNotifications;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Messages;
using MovieApp.Application.Models.PushNotifications;
using MovieApp.Application.Services.Identity;
using MovieApp.Application.Services.PushNotifications;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Social;

namespace MovieApp.Infrastructure.Messages;

public sealed class DirectMessageService(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IUserAvatarPresentationService avatarPresentationService,
    IExpoPushClient expoPushClient,
    IOptions<PushNotificationsOptions> pushOptions,
    ILogger<DirectMessageService> logger) : IDirectMessageService
{
    public const string NotificationType = "DirectMessage";
    public const string PushDataType = "direct-message";

    private static readonly TimeSpan PushTimeout = TimeSpan.FromSeconds(8);

    public async Task<DirectConversationPage> ListConversationsAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 50);
        // Order by entity columns in the join (before DTO projection) so EF can translate COALESCE.
        var ordered = VisibleConversationRows(userId, orderForList: true);
        var total = await ordered.CountAsync(cancellationToken);
        var rows = await ordered
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync(cancellationToken);

        var conversationIds = rows.Select(row => row.ConversationId).ToArray();
        var previews = await LoadLatestPreviewsAsync(conversationIds, cancellationToken);
        var unread = await LoadUnreadCountsAsync(userId, conversationIds, cancellationToken);
        var avatars = await LoadAvatarsAsync(rows.Select(row => row.PeerId), cancellationToken);

        var items = rows.Select(row => new DirectConversationListItem(
            row.ConversationId,
            ToUser(row, avatars),
            previews.GetValueOrDefault(row.ConversationId),
            unread.GetValueOrDefault(row.ConversationId),
            row.UpdatedAt,
            row.LastMessageAt)).ToList();

        return new DirectConversationPage(items, safePage, safeSize, safePage * safeSize < total);
    }

    public async Task<DirectConversationDetail> GetConversationAsync(
        Guid conversationId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var row = await FindVisibleConversationRowAsync(userId, conversationId, cancellationToken)
            ?? throw new NotFoundException("Conversation not found.");
        var avatars = await LoadAvatarsAsync([row.PeerId], cancellationToken);
        return ToDetail(row, avatars);
    }

    public async Task<DirectConversationDetail> GetOrCreateConversationAsync(
        Guid otherUserId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (otherUserId == Guid.Empty)
        {
            throw new NotFoundException("User not found.");
        }

        if (otherUserId == userId)
        {
            throw new ConflictException("You cannot message yourself.");
        }

        var otherExists = await dbContext.Users.AsNoTracking()
            .AnyAsync(user => user.Id == otherUserId && user.IsActive, cancellationToken);
        if (!otherExists)
        {
            throw new NotFoundException("User not found.");
        }

        var conversationId = await dbContext.Database.ExecuteInRetriableTransactionAsync(async ct =>
        {
            await SocialPairLock.AcquireAsync(dbContext, userId, otherUserId, ct);
            if (await IsBlockedEitherWayAsync(userId, otherUserId, ct))
            {
                throw new NotFoundException("User not found.");
            }

            var existingId = await FindConversationIdAsync(userId, otherUserId, ct);
            if (existingId != Guid.Empty)
            {
                return existingId;
            }

            var conversation = DirectConversation.Create(DateTime.UtcNow);
            dbContext.DirectConversations.Add(conversation);
            dbContext.DirectConversationParticipants.Add(
                DirectConversationParticipant.Create(conversation.Id, userId));
            dbContext.DirectConversationParticipants.Add(
                DirectConversationParticipant.Create(conversation.Id, otherUserId));
            await dbContext.SaveChangesAsync(ct);
            return conversation.Id;
        }, cancellationToken);

        return await GetConversationAsync(conversationId, cancellationToken);
    }

    public async Task<DirectMessagePage> GetMessagesAsync(
        Guid conversationId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 50);
        var access = await RequireVisibleConversationAsync(userId, conversationId, cancellationToken);

        var clearedAt = await GetHistoryClearedAtAsync(userId, conversationId, cancellationToken);
        var visibleMessages = dbContext.DirectMessages.AsNoTracking()
            .Where(message => message.ConversationId == conversationId);
        if (clearedAt is { } cutoff)
        {
            visibleMessages = visibleMessages.Where(message => message.CreatedAt > cutoff);
        }

        var total = await visibleMessages.CountAsync(cancellationToken);
        var window = DirectMessagePageWindow.Resolve(total, safePage, safeSize);
        var items = window.Take == 0
            ? []
            : await visibleMessages
                .OrderBy(message => message.CreatedAt)
                .ThenBy(message => message.Id)
                .Skip(window.Skip)
                .Take(window.Take)
                .Select(message => new DirectMessageItem(
                    message.Id, message.ConversationId, message.SenderId, message.Body, message.CreatedAt))
                .ToListAsync(cancellationToken);

        await MarkReadAsync(userId, access.PeerId, conversationId, cancellationToken);
        return new DirectMessagePage(items, safePage, safeSize, window.HasMore);
    }

    public async Task HideConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        await RequireVisibleConversationAsync(userId, conversationId, cancellationToken);

        var participant = await dbContext.DirectConversationParticipants
            .FirstOrDefaultAsync(item => item.ConversationId == conversationId && item.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Conversation not found.");

        participant.ClearHistory(DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DirectMessageItem> SendAsync(
        Guid conversationId, string? body, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var normalized = NormalizeBody(body);
        var sent = await dbContext.Database.ExecuteInRetriableTransactionAsync(async ct =>
        {
            var access = await RequireVisibleConversationAsync(userId, conversationId, ct);
            await SocialPairLock.AcquireAsync(dbContext, userId, access.PeerId, ct);
            if (await IsBlockedEitherWayAsync(userId, access.PeerId, ct))
            {
                throw new NotFoundException("Conversation not found.");
            }

            var conversation = await dbContext.DirectConversations
                .FirstAsync(item => item.Id == conversationId, ct);
            var now = DateTime.UtcNow;
            var message = DirectMessage.Create(conversationId, userId, normalized, now);
            conversation.Touch(now);
            dbContext.DirectMessages.Add(message);

            var unreadNoticeUpdated = await dbContext.SocialNotifications
                .Where(notification => notification.RecipientId == access.PeerId
                    && notification.ActorId == userId
                    && notification.NotificationType == NotificationType
                    && notification.ReadAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(notification => notification.CreatedAt, now),
                    ct);
            if (unreadNoticeUpdated == 0)
            {
                dbContext.SocialNotifications.Add(SocialNotification.Create(
                    access.PeerId, userId, NotificationType, null, null, null, now));
            }

            await dbContext.SaveChangesAsync(ct);
            return new SentDirectMessage(message.Id, conversationId, userId, message.Body, message.CreatedAt, access.PeerId);
        }, cancellationToken);

        await TryNotifyPeerAsync(sent, cancellationToken);
        return new DirectMessageItem(sent.Id, sent.ConversationId, sent.SenderId, sent.Body, sent.CreatedAt);
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        return await (
            from participant in dbContext.DirectConversationParticipants.AsNoTracking()
            join peerParticipant in dbContext.DirectConversationParticipants.AsNoTracking()
                on participant.ConversationId equals peerParticipant.ConversationId
            join peer in dbContext.Users.AsNoTracking() on peerParticipant.UserId equals peer.Id
            join message in dbContext.DirectMessages.AsNoTracking()
                on participant.ConversationId equals message.ConversationId
            where participant.UserId == userId
                && peerParticipant.UserId != userId
                && peer.IsActive
                && message.SenderId != userId
                && (participant.LastReadAt == null || message.CreatedAt > participant.LastReadAt)
                && (participant.HistoryClearedAt == null || message.CreatedAt > participant.HistoryClearedAt)
                && !dbContext.UserBlocks.Any(block =>
                    (block.BlockerId == userId && block.BlockedUserId == peer.Id)
                    || (block.BlockerId == peer.Id && block.BlockedUserId == userId))
            select message.Id).CountAsync(cancellationToken);
    }

    private async Task<DateTime?> GetHistoryClearedAtAsync(
        Guid userId, Guid conversationId, CancellationToken cancellationToken) =>
        await dbContext.DirectConversationParticipants.AsNoTracking()
            .Where(item => item.ConversationId == conversationId && item.UserId == userId)
            .Select(item => item.HistoryClearedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task MarkReadAsync(
        Guid userId, Guid peerId, Guid conversationId, CancellationToken cancellationToken)
    {
        var newest = await dbContext.DirectMessages.AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .MaxAsync(message => (DateTime?)message.CreatedAt, cancellationToken);
        if (newest is null)
        {
            return;
        }

        var participant = await dbContext.DirectConversationParticipants
            .FirstOrDefaultAsync(item => item.ConversationId == conversationId && item.UserId == userId, cancellationToken);
        if (participant is null)
        {
            return;
        }

        var advanced = participant.LastReadAt is null || participant.LastReadAt < newest;
        if (advanced)
        {
            participant.MarkRead(newest.Value);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!advanced)
        {
            return;
        }

        var readAt = DateTime.UtcNow;
        await dbContext.SocialNotifications
            .Where(notification => notification.RecipientId == userId
                && notification.ActorId == peerId
                && notification.NotificationType == NotificationType
                && notification.ReadAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(notification => notification.ReadAt, readAt),
                cancellationToken);
    }

    private async Task TryNotifyPeerAsync(SentDirectMessage sent, CancellationToken cancellationToken)
    {
        if (!pushOptions.Value.Enabled)
        {
            return;
        }

        try
        {
            var sender = await dbContext.Users.AsNoTracking()
                .Where(user => user.Id == sent.SenderId)
                .Select(user => new { user.DisplayName, user.UserName })
                .FirstOrDefaultAsync(cancellationToken);
            var devices = await dbContext.PushDevices
                .Where(device => device.UserId == sent.PeerId && device.IsActive && device.ExpoPushToken != "")
                .ToListAsync(cancellationToken);
            if (devices.Count == 0)
            {
                return;
            }

            var title = string.IsNullOrWhiteSpace(sender?.DisplayName)
                ? sender?.UserName ?? "Movie Cave"
                : sender.DisplayName;
            var data = new Dictionary<string, string>
            {
                ["type"] = PushDataType,
                ["conversationId"] = sent.ConversationId.ToString()
            };
            var messages = devices
                .Select(device => new PushNotificationMessage(
                    Guid.NewGuid(), device.ExpoPushToken, title, Preview(sent.Body), data))
                .ToArray();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(PushTimeout);
            var results = await expoPushClient.SendAsync(messages, timeout.Token);
            var utcNow = DateTime.UtcNow;
            var deactivated = false;
            for (var index = 0; index < results.Count && index < devices.Count; index++)
            {
                var result = results[index];
                if (result.IsSuccess)
                {
                    continue;
                }

                if (ExpoPushErrorClassifier.IsPermanentDeviceError(result.ErrorCode))
                {
                    devices[index].IsActive = false;
                    devices[index].UpdatedAtUtc = utcNow;
                    deactivated = true;
                }
            }

            if (deactivated)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            if (results.Any(result => !result.IsSuccess))
            {
                logger.LogWarning(
                    "Direct message push was rejected for conversation {ConversationId}.",
                    sent.ConversationId);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Direct message push failed for conversation {ConversationId}.", sent.ConversationId);
        }
    }


    private IQueryable<ConversationRow> VisibleConversationRows(Guid userId, bool orderForList = false)
    {
        if (orderForList)
        {
            return
                from participant in dbContext.DirectConversationParticipants.AsNoTracking()
                join conversation in dbContext.DirectConversations.AsNoTracking()
                    on participant.ConversationId equals conversation.Id
                join peerParticipant in dbContext.DirectConversationParticipants.AsNoTracking()
                    on conversation.Id equals peerParticipant.ConversationId
                join peer in dbContext.Users.AsNoTracking() on peerParticipant.UserId equals peer.Id
                where participant.UserId == userId
                    && peerParticipant.UserId != userId
                    && peer.IsActive
                    && (participant.HistoryClearedAt == null || conversation.LastMessageAt > participant.HistoryClearedAt)
                    && !dbContext.UserBlocks.Any(block =>
                        (block.BlockerId == userId && block.BlockedUserId == peer.Id)
                        || (block.BlockerId == peer.Id && block.BlockedUserId == userId))
                orderby conversation.LastMessageAt ?? conversation.UpdatedAt descending,
                    conversation.Id descending
                select new ConversationRow(
                    conversation.Id,
                    conversation.CreatedAt,
                    conversation.UpdatedAt,
                    conversation.LastMessageAt,
                    peer.Id,
                    peer.UserName,
                    peer.DisplayName);
        }

        return
            from participant in dbContext.DirectConversationParticipants.AsNoTracking()
            join conversation in dbContext.DirectConversations.AsNoTracking()
                on participant.ConversationId equals conversation.Id
            join peerParticipant in dbContext.DirectConversationParticipants.AsNoTracking()
                on conversation.Id equals peerParticipant.ConversationId
            join peer in dbContext.Users.AsNoTracking() on peerParticipant.UserId equals peer.Id
            where participant.UserId == userId
                && peerParticipant.UserId != userId
                && peer.IsActive
                && (participant.HistoryClearedAt == null || conversation.LastMessageAt > participant.HistoryClearedAt)
                && !dbContext.UserBlocks.Any(block =>
                    (block.BlockerId == userId && block.BlockedUserId == peer.Id)
                    || (block.BlockerId == peer.Id && block.BlockedUserId == userId))
            select new ConversationRow(
                conversation.Id,
                conversation.CreatedAt,
                conversation.UpdatedAt,
                conversation.LastMessageAt,
                peer.Id,
                peer.UserName,
                peer.DisplayName);
    }

    private async Task<ConversationRow?> FindVisibleConversationRowAsync(
        Guid userId, Guid conversationId, CancellationToken cancellationToken) =>
        await (
            from participant in dbContext.DirectConversationParticipants.AsNoTracking()
            join conversation in dbContext.DirectConversations.AsNoTracking()
                on participant.ConversationId equals conversation.Id
            join peerParticipant in dbContext.DirectConversationParticipants.AsNoTracking()
                on conversation.Id equals peerParticipant.ConversationId
            join peer in dbContext.Users.AsNoTracking() on peerParticipant.UserId equals peer.Id
            where participant.UserId == userId
                && peerParticipant.UserId != userId
                && peer.IsActive
                && conversation.Id == conversationId
                && !dbContext.UserBlocks.Any(block =>
                    (block.BlockerId == userId && block.BlockedUserId == peer.Id)
                    || (block.BlockerId == peer.Id && block.BlockedUserId == userId))
            select new ConversationRow(
                conversation.Id,
                conversation.CreatedAt,
                conversation.UpdatedAt,
                conversation.LastMessageAt,
                peer.Id,
                peer.UserName,
                peer.DisplayName)
        ).FirstOrDefaultAsync(cancellationToken);

    private async Task<ConversationRow> RequireVisibleConversationAsync(
        Guid userId, Guid conversationId, CancellationToken cancellationToken) =>
        await FindVisibleConversationRowAsync(userId, conversationId, cancellationToken)
        ?? throw new NotFoundException("Conversation not found.");

    private Task<Guid> FindConversationIdAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken) =>
        (
            from mine in dbContext.DirectConversationParticipants.AsNoTracking()
            join theirs in dbContext.DirectConversationParticipants.AsNoTracking()
                on mine.ConversationId equals theirs.ConversationId
            where mine.UserId == userId && theirs.UserId == otherUserId
            select mine.ConversationId
        ).FirstOrDefaultAsync(cancellationToken);

    private async Task<Dictionary<Guid, DirectMessagePreview>> LoadLatestPreviewsAsync(
        Guid[] conversationIds, CancellationToken cancellationToken)
    {
        if (conversationIds.Length == 0)
        {
            return [];
        }

        var rows = await dbContext.DirectMessages.AsNoTracking()
            .Where(message => conversationIds.Contains(message.ConversationId))
            .Where(message => message.CreatedAt == dbContext.DirectMessages
                .Where(inner => inner.ConversationId == message.ConversationId)
                .Max(inner => (DateTime?)inner.CreatedAt))
            .Select(message => new
            {
                message.ConversationId,
                message.Id,
                Preview = new DirectMessagePreview(message.Id, message.SenderId, message.Body, message.CreatedAt)
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.ConversationId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(row => row.Id).First().Preview);
    }

    private async Task<Dictionary<Guid, int>> LoadUnreadCountsAsync(
        Guid userId, Guid[] conversationIds, CancellationToken cancellationToken)
    {
        if (conversationIds.Length == 0)
        {
            return [];
        }

        var rows = await dbContext.DirectMessages.AsNoTracking()
            .Where(message => conversationIds.Contains(message.ConversationId) && message.SenderId != userId)
            .Where(message => dbContext.DirectConversationParticipants.Any(participant =>
                participant.ConversationId == message.ConversationId
                && participant.UserId == userId
                && (participant.LastReadAt == null || message.CreatedAt > participant.LastReadAt)
                && (participant.HistoryClearedAt == null || message.CreatedAt > participant.HistoryClearedAt)))
            .GroupBy(message => message.ConversationId)
            .Select(group => new { ConversationId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.ConversationId, row => row.Count);
    }

    private async Task<IReadOnlyDictionary<Guid, string?>> LoadAvatarsAsync(
        IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string?>();
        }

        var avatars = await avatarPresentationService.GetForUsersAsync(ids, cancellationToken);
        return avatars.ToDictionary(
            pair => pair.Key,
            pair => (string?)pair.Value.EffectiveAvatarUrl);
    }

    private static DirectMessageUserSummary ToUser(
        ConversationRow row, IReadOnlyDictionary<Guid, string?> avatars) =>
        new(row.PeerId, row.UserName, row.DisplayName, avatars.GetValueOrDefault(row.PeerId));

    private static DirectConversationDetail ToDetail(
        ConversationRow row, IReadOnlyDictionary<Guid, string?> avatars) =>
        new(row.ConversationId, ToUser(row, avatars), row.CreatedAt, row.UpdatedAt, row.LastMessageAt);

    private Task<bool> IsBlockedEitherWayAsync(Guid first, Guid second, CancellationToken cancellationToken) =>
        dbContext.UserBlocks.AsNoTracking().AnyAsync(block =>
            (block.BlockerId == first && block.BlockedUserId == second)
            || (block.BlockerId == second && block.BlockedUserId == first), cancellationToken);

    private static string NormalizeBody(string? body)
    {
        var normalized = body?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new ValidationException("Message body is required.");
        }

        if (normalized.Length > DirectMessage.MaxBodyLength)
        {
            throw new ValidationException("Message cannot exceed 2000 characters.");
        }

        return normalized;
    }

    private static string Preview(string body)
    {
        var compact = string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        const int max = 140;
        return compact.Length <= max ? compact : string.Concat(compact.AsSpan(0, max).TrimEnd(), "…");
    }

    private Guid GetUserId()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty)
        {
            throw new AuthenticationException("Authentication is required.");
        }

        return userId;
    }

    private sealed record ConversationRow(
        Guid ConversationId,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DateTime? LastMessageAt,
        Guid PeerId,
        string UserName,
        string DisplayName);

    private sealed record SentDirectMessage(
        Guid Id,
        Guid ConversationId,
        Guid SenderId,
        string Body,
        DateTime CreatedAt,
        Guid PeerId);
}
