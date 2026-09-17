namespace MovieApp.Application.Services.Notifications;

public interface IDeleteNotificationService
{
    Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default);
}
