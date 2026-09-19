using Microsoft.Extensions.Hosting;

namespace MovieApp.Infrastructure.Identity;

public static class PasswordResetDeliveryConfiguration
{
    internal const string BackgroundJobsRequiredMessage =
        "Password reset delivery requires BackgroundJobs.Enabled in non-Development environments so Hangfire can process delivery jobs durably.";

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
