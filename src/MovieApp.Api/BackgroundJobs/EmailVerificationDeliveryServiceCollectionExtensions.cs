using Microsoft.Extensions.Hosting;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.Identity;
using MovieApp.Infrastructure.Identity;

namespace MovieApp.Api.BackgroundJobs;

public static class EmailVerificationDeliveryServiceCollectionExtensions
{
    public static IServiceCollection AddEmailVerificationDelivery(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        services.AddDataProtection();
        services.AddSingleton<IEmailVerificationDeliverySecretProtector, DataProtectionEmailVerificationDeliverySecretProtector>();
        services.AddScoped<IEmailVerificationDeliveryService, EmailVerificationDeliveryService>();

        var backgroundJobsEnabled = configuration
            .GetSection(BackgroundJobsOptions.SectionName)
            .GetValue<bool>(nameof(BackgroundJobsOptions.Enabled));

        if (backgroundJobsEnabled)
        {
            services.AddScoped<EmailVerificationDeliveryJob>();
            services.AddSingleton<IEmailVerificationDeliveryEnqueuer, HangfireEmailVerificationDeliveryEnqueuer>();
        }
        else if (hostEnvironment.IsEnvironment("Testing"))
        {
            services.AddSingleton<IEmailVerificationDeliveryEnqueuer, SynchronousEmailVerificationDeliveryEnqueuer>();
        }
        else
        {
            services.AddSingleton<IEmailVerificationDeliveryEnqueuer, BackgroundEmailVerificationDeliveryEnqueuer>();
        }

        return services;
    }
}
