using Microsoft.Extensions.Hosting;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.Identity;
using MovieApp.Infrastructure.Email;
using MovieApp.Infrastructure.Identity;

namespace MovieApp.Api.BackgroundJobs;

public static class PasswordResetDeliveryServiceCollectionExtensions
{
    public static IServiceCollection AddPasswordResetDelivery(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        services.AddScoped<IPasswordResetDeliveryService, PasswordResetDeliveryService>();

        if (hostEnvironment.IsEnvironment("Testing"))
        {
            services.AddSingleton<IPasswordResetEmailSender>(serviceProvider =>
                serviceProvider.GetRequiredService<CapturingEmailSender>());
        }
        else
        {
            services
                .AddHttpClient<IPasswordResetEmailSender, ResendPasswordResetEmailSender>(client =>
                {
                    client.BaseAddress = new Uri("https://api.resend.com/");
                });
        }

        var backgroundJobsEnabled = configuration
            .GetSection(BackgroundJobsOptions.SectionName)
            .GetValue<bool>(nameof(BackgroundJobsOptions.Enabled));

        PasswordResetDeliveryConfiguration.EnsureSupportedDeliveryBackend(
            hostEnvironment,
            backgroundJobsEnabled);

        if (hostEnvironment.IsEnvironment("Testing"))
        {
            services.AddSingleton<IPasswordResetDeliveryEnqueuer, SynchronousPasswordResetDeliveryEnqueuer>();
        }
        else if (backgroundJobsEnabled)
        {
            services.AddScoped<PasswordResetDeliveryJob>();
            services.AddSingleton<IPasswordResetDeliveryEnqueuer, HangfirePasswordResetDeliveryEnqueuer>();
        }
        else
        {
            services.AddSingleton<IPasswordResetDeliveryEnqueuer, BackgroundPasswordResetDeliveryEnqueuer>();
        }

        return services;
    }
}
