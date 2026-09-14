namespace MovieApp.Application.Models.PushNotifications;

public sealed record ExpoPushReceiptResult(
    string TicketId,
    bool IsDelivered,
    string? ErrorCode,
    string? ErrorMessage,
    bool IsPermanentFailure,
    bool IsRetryableFailure);
