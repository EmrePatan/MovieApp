using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Services.Identity;

namespace MovieApp.Api.BackgroundJobs;

public sealed class HangfireEmailVerificationDeliveryEnqueuer(
    IBackgroundJobClient backgroundJobClient,
    ILogger<HangfireEmailVerificationDeliveryEnqueuer> logger) : IEmailVerificationDeliveryEnqueuer
{
    public Task EnqueueAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            backgroundJobClient.Enqueue<EmailVerificationDeliveryJob>(
                job => job.ExecuteAsync(tokenId));

            EmailVerificationLogMessages.LogDeliveryEnqueued(logger, tokenId);
        }
        catch (Exception exception)
        {
            EmailVerificationLogMessages.LogDeliveryEnqueueFailed(
                logger,
                tokenId,
                Guid.Empty,
                exception.GetType().Name);

            throw;
        }

        return Task.CompletedTask;
    }
}
