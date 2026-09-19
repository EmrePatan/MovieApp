using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Services.Identity;

namespace MovieApp.Api.BackgroundJobs;

public sealed class HangfirePasswordResetDeliveryEnqueuer(
    IBackgroundJobClient backgroundJobClient,
    ILogger<HangfirePasswordResetDeliveryEnqueuer> logger) : IPasswordResetDeliveryEnqueuer
{
    public Task EnqueueAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            backgroundJobClient.Enqueue<PasswordResetDeliveryJob>(
                job => job.ExecuteAsync(tokenId));

            PasswordResetLogMessages.LogDeliveryEnqueued(logger, tokenId);
        }
        catch (Exception exception)
        {
            PasswordResetLogMessages.LogDeliveryEnqueueFailed(
                logger,
                tokenId,
                Guid.Empty,
                exception.GetType().Name);

            throw;
        }

        return Task.CompletedTask;
    }
}
