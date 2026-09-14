using MovieApp.Application.Models.PushNotifications;

namespace MovieApp.Application.Abstractions.PushNotifications;

public interface IExpoPushClient
{
    Task<IReadOnlyList<ExpoPushSendResult>> SendAsync(
        IReadOnlyList<PushNotificationMessage> messages,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpoPushReceiptResult>> GetReceiptsAsync(
        IReadOnlyCollection<string> ticketIds,
        CancellationToken cancellationToken = default);
}
