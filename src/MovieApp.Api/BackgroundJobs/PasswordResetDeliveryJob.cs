using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Identity;

namespace MovieApp.Api.BackgroundJobs;

public sealed class PasswordResetDeliveryJob(
    IPasswordResetDeliveryService deliveryService,
    ILogger<PasswordResetDeliveryJob> logger)
{
    [AutomaticRetry(Attempts = 5)]
    public Task ExecuteAsync(Guid tokenId) =>
        BackgroundJobOperationalRunner.RunAsync(
            logger,
            $"password-reset-delivery:{tokenId}",
            () => deliveryService.DeliverAsync(tokenId));
}
