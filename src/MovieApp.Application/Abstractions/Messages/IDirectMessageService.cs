namespace MovieApp.Application.Abstractions.Messages;

public sealed record DirectMessageUserSummary(Guid Id, string UserName, string DisplayName, string? AvatarUrl);

public sealed record DirectMessagePreview(Guid Id, Guid SenderId, string Body, DateTime CreatedAt);

public sealed record DirectConversationListItem(
    Guid Id,
    DirectMessageUserSummary OtherUser,
    DirectMessagePreview? LastMessage,
    int UnreadCount,
    DateTime UpdatedAt,
    DateTime? LastMessageAt);

public sealed record DirectConversationPage(
    IReadOnlyList<DirectConversationListItem> Items,
    int Page,
    int PageSize,
    bool HasMore);

public sealed record DirectConversationDetail(
    Guid Id,
    DirectMessageUserSummary OtherUser,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastMessageAt);

public sealed record DirectMessageItem(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string Body,
    DateTime CreatedAt);

public sealed record DirectMessagePage(
    IReadOnlyList<DirectMessageItem> Items,
    int Page,
    int PageSize,
    bool HasMore);

public interface IDirectMessageService
{
    Task<DirectConversationPage> ListConversationsAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<DirectConversationDetail> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<DirectConversationDetail> GetOrCreateConversationAsync(Guid otherUserId, CancellationToken cancellationToken = default);

    Task<DirectMessagePage> GetMessagesAsync(Guid conversationId, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<DirectMessageItem> SendAsync(Guid conversationId, string? body, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);
}
