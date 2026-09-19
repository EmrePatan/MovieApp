using Microsoft.Extensions.Hosting;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.Identity;
using MovieApp.Infrastructure.Email;
using MovieApp.Infrastructure.Identity;

namespace MovieApp.Api.BackgroundJobs;

public static class EmailVerificationDeliveryServiceCollectionExtensions
{
    public static IServiceCollection AddEmailVerificationDelivery(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        services.AddScoped<IEmailVerificationDeliveryService, EmailVerificationDeliveryService>();

        if (hostEnvironment.IsEnvironment("Testing"))
        {
            services.AddSingleton<IEmailVerificationEmailSender>(serviceProvider =>
                serviceProvider.GetRequiredService<CapturingEmailSender>());
        }
        else
        {
            services
                .AddHttpClient<IEmailVerificationEmailSender, ResendVerificationEmailSender>(client =>
                {
                    client.BaseAddress = new Uri("https://api.resend.com/");
                });
        }

        var backgroundJobsEnabled = configuration
            .GetSection(BackgroundJobsOptions.SectionName)
            .GetValue<bool>(nameof(BackgroundJobsOptions.Enabled));

        EmailVerificationDeliveryConfiguration.EnsureSupportedDeliveryBackend(
            hostEnvironment,
            backgroundJobsEnabled);

        if (hostEnvironment.IsEnvironment("Testing"))
        {
            services.AddSingleton<IEmailVerificationDeliveryEnqueuer, SynchronousEmailVerificationDeliveryEnqueuer>();
        }
        else if (backgroundJobsEnabled)
        {
            services.AddScoped<EmailVerificationDeliveryJob>();
            services.AddSingleton<IEmailVerificationDeliveryEnqueuer, HangfireEmailVerificationDeliveryEnqueuer>();
        }
        else
        {
            services.AddSingleton<IEmailVerificationDeliveryEnqueuer, BackgroundEmailVerificationDeliveryEnqueuer>();
        }

        return services;
    }
}
