using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Api.BackgroundJobs;

public static class BackgroundJobsServiceCollectionExtensions
{
    public static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BackgroundJobsOptions>(configuration.GetSection(BackgroundJobsOptions.SectionName));

        var backgroundJobs = configuration
            .GetSection(BackgroundJobsOptions.SectionName)
            .Get<BackgroundJobsOptions>() ?? new BackgroundJobsOptions();

        if (!backgroundJobs.Enabled)
        {
            services.AddSingleton<IRecurringBackgroundJobRegistrar, NoOpRecurringBackgroundJobRegistrar>();
            return services;
        }

        services.AddSingleton<IRecurringBackgroundJobRegistrar, HangfireRecurringBackgroundJobRegistrar>();

        var postgreSqlOptions = configuration
            .GetSection(PostgreSqlOptions.SectionName)
            .Get<PostgreSqlOptions>() ?? new PostgreSqlOptions();

        var connectionString = postgreSqlOptions.IsConfigured()
            ? postgreSqlOptions.ResolveConnectionString()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "BackgroundJobs.Enabled requires PostgreSql:ConnectionString to be configured.");
        }

        services.AddHangfire(config =>
        {
            config.UseSimpleAssemblyNameTypeSerializer();
            config.UseRecommendedSerializerSettings();
            config.UsePostgreSqlStorage(
                options => options.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions
                {
                    SchemaName = "hangfire"
                });
        });

        services.AddHangfireServer();
        services.AddHostedService<RecurringBackgroundJobsStartup>();

        services.AddScoped<TmdbTvChangesSyncJob>();
        services.AddScoped<HotReleaseCheckJob>();
        services.AddScoped<MovieReleaseCheckJob>();
        services.AddScoped<ReleaseNotificationFanoutJob>();
        services.AddScoped<PushDeliveryPreparationJob>();
        services.AddScoped<PushDispatchJob>();
        services.AddScoped<PushReceiptJob>();

        return services;
    }
}
