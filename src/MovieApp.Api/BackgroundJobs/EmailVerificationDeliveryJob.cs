using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Identity;

namespace MovieApp.Api.BackgroundJobs;

public sealed class EmailVerificationDeliveryJob(
    IEmailVerificationDeliveryService deliveryService,
    ILogger<EmailVerificationDeliveryJob> logger)
{
    [AutomaticRetry(Attempts = 5)]
    public Task ExecuteAsync(Guid tokenId) =>
        BackgroundJobOperationalRunner.RunAsync(
            logger,
            $"email-verification-delivery:{tokenId}",
            () => deliveryService.DeliverAsync(tokenId));
}
