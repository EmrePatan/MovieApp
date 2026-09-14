namespace MovieApp.Application.Models.PushNotifications;

public sealed record ExpoPushSendResult(
    Guid DeliveryId,
    bool IsSuccess,
    string? TicketId,
    string? ErrorCode,
    string? ErrorMessage,
    bool IsPermanentFailure,
    bool IsRetryableFailure);
