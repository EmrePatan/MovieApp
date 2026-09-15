namespace MovieApp.Contracts.Notifications;

public sealed record NotificationsResponse(
    IReadOnlyList<NotificationItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
