namespace MovieApp.Application.Models.Notifications;

public sealed record NotificationsListResult(
    IReadOnlyList<NotificationInboxItemResult> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
