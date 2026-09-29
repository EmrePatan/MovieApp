namespace MovieApp.Application.Abstractions.Identity;

public interface IAccountEmailChangedNotificationSender
{
    Task SendAsync(
        string previousEmail,
        string newEmail,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
