namespace MovieApp.Application.Services.Notifications;

public interface IGetUnreadNotificationCountService
{
    Task<int> GetAsync(CancellationToken cancellationToken = default);
}
