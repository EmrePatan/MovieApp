using Microsoft.Extensions.Hosting;

namespace MovieApp.Infrastructure.Identity;

public static class EmailVerificationDeliveryConfiguration
{
    internal const string BackgroundJobsRequiredMessage =
        "Email verification delivery requires BackgroundJobs.Enabled in non-Development environments so Hangfire can process delivery jobs durably.";

    public static void EnsureSupportedDeliveryBackend(
        IHostEnvironment hostEnvironment,
        bool backgroundJobsEnabled)
    {
        if (hostEnvironment.IsEnvironment("Testing") || hostEnvironment.IsDevelopment())
        {
            return;
        }

        if (!backgroundJobsEnabled)
        {
            throw new InvalidOperationException(BackgroundJobsRequiredMessage);
        }
    }
}
