using MovieApp.Application.Abstractions.Identity;

namespace MovieApp.Infrastructure.Email;

public sealed class NoOpAccountEmailChangedNotificationSender : IAccountEmailChangedNotificationSender
{
    public Task SendAsync(
        string previousEmail,
        string newEmail,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
