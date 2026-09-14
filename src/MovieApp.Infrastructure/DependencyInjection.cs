using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Infrastructure.Caching;
using MovieApp.Infrastructure.Configuration;
using MovieApp.Infrastructure.Email;
using MovieApp.Infrastructure.Identity;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;
using MovieApp.Application.Abstractions.RateLimiting;
using MovieApp.Infrastructure.PushNotifications;
using MovieApp.Infrastructure.Providers;
using MovieApp.Infrastructure.RateLimiting;
using StackExchange.Redis;

namespace MovieApp.Infrastructure;

public static class DependencyInjection

{

    public static IServiceCollection AddInfrastructure(

        this IServiceCollection services,

        IConfiguration configuration)

    {

        services.AddOptions<PostgreSqlOptions>()
            .Bind(configuration.GetSection(PostgreSqlOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<PostgreSqlOptions>, PostgreSqlOptionsValidator>();

        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<RedisOptions>, RedisOptionsValidator>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        services.Configure<RecommendationOptions>(configuration.GetSection(RecommendationOptions.SectionName));

        services.Configure<HomeOptions>(configuration.GetSection(HomeOptions.SectionName));

        services.Configure<PushNotificationsOptions>(configuration.GetSection(PushNotificationsOptions.SectionName));

        services.AddOptions<SearchOptions>()
            .Bind(configuration.GetSection(SearchOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<SearchOptions>, SearchOptionsValidator>();

        services.Configure<SmtpEmailOptions>(configuration.GetSection(SmtpEmailOptions.SectionName));

        services.AddOptions<PasswordResetOptions>()

            .Bind(configuration.GetSection(PasswordResetOptions.SectionName))

            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<PasswordResetOptions>, PasswordResetOptionsValidator>();

        services.AddMovieDataProviders(configuration);

        services.AddTvShowDataProviders(configuration);

        services.AddDetailProviders(configuration);



        var postgreSqlOptions = configuration

            .GetSection(PostgreSqlOptions.SectionName)

            .Get<PostgreSqlOptions>() ?? new PostgreSqlOptions();



        var redisOptions = configuration

            .GetSection(RedisOptions.SectionName)

            .Get<RedisOptions>() ?? new RedisOptions();



        var postgreSqlConnectionString = postgreSqlOptions.IsConfigured()

            ? postgreSqlOptions.ResolveConnectionString()

            : string.Empty;



        services.AddDbContext<ApplicationDbContext>(options =>

            options.UseNpgsql(postgreSqlConnectionString));



        services.AddScoped<IApplicationDbContext>(provider =>

            provider.GetRequiredService<ApplicationDbContext>());



        services.AddScoped<IMovieRepository, MovieRepository>();

        services.AddScoped<IPersonRepository, PersonRepository>();

        services.AddScoped<ITvShowRepository, TvShowRepository>();

        services.AddScoped<ISeasonRepository, SeasonRepository>();

        services.AddScoped<IEpisodeRepository, EpisodeRepository>();

        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<IUserStatisticsRepository, UserStatisticsRepository>();

        services.AddScoped<IFavoriteRepository, FavoriteRepository>();

        services.AddScoped<ITvShowFollowRepository, TvShowFollowRepository>();

        services.AddScoped<ITvShowCatalogSyncStateRepository, TvShowCatalogSyncStateRepository>();

        services.AddScoped<ITmdbTvChangesSyncCheckpointRepository, TmdbTvChangesSyncCheckpointRepository>();

        services.AddScoped<IFollowedTvShowCatalogRepository, FollowedTvShowCatalogRepository>();

        services.AddScoped<IHotReleaseCandidateRepository, HotReleaseCandidateRepository>();

        services.AddScoped<ICatalogReleaseEventRepository, CatalogReleaseEventRepository>();

        services.AddScoped<IReleaseNotificationFanoutRepository, ReleaseNotificationFanoutRepository>();

        services.AddScoped<IPushDeviceRepository, PushDeviceRepository>();

        services.AddScoped<IPushNotificationDeliveryRepository, PushNotificationDeliveryRepository>();

        services.AddExpoPushClient();

        services.AddScoped<IReleaseDetectionCatalogRepository, ReleaseDetectionCatalogRepository>();

        services.AddScoped<IWatchlistRepository, WatchlistRepository>();

        services.AddScoped<IWatchlistItemRepository, WatchlistItemRepository>();

        services.AddScoped<IRatingRepository, RatingRepository>();

        services.AddScoped<IReviewRepository, ReviewRepository>();

        services.AddScoped<IWatchedMovieRepository, WatchedMovieRepository>();

        services.AddScoped<IWatchedEpisodeRepository, WatchedEpisodeRepository>();

        services.AddScoped<ISearchRepository, SearchRepository>();

        services.AddScoped<ISearchHistoryRepository, SearchHistoryRepository>();

        services.AddScoped<ISearchProviderRefreshRepository, SearchProviderRefreshRepository>();

        services.AddScoped<IRecommendationRepository, RecommendationRepository>();

        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();

        services.AddSingleton<InMemoryRateLimitCounterStore>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        services.AddScoped<ITokenService, JwtTokenService>();

        services.AddSingleton<CapturingEmailSender>();

        services.AddSingleton<DevelopmentEmailSender>();

        services.AddSingleton<SmtpEmailSender>();

        services.AddSingleton<IEmailSender>(ResolveEmailSender);



        services.AddSingleton<RedisCacheFailureLogger>();

        ConfigurationOptions? redisConfigurationOptions = null;

        if (!string.IsNullOrWhiteSpace(redisOptions.ConnectionString))

        {

            redisConfigurationOptions = RedisConnectionOptionsFactory.Create(redisOptions);

            services.AddStackExchangeRedisCache(options =>

            {

                options.ConfigurationOptions = redisConfigurationOptions;

                options.InstanceName = redisOptions.InstanceName;

            });

            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfigurationOptions!));

            services.AddSingleton<LocalSearchRefreshSingleFlightGate>();
            services.AddSingleton<LocalSearchRefreshCompletionRegistry>();
            services.AddSingleton<SearchRefreshLockDiagnostics>();
            services.AddSingleton<ISearchRefreshLockDiagnostics>(provider =>
                provider.GetRequiredService<SearchRefreshLockDiagnostics>());
            services.AddSingleton<ISearchRefreshLockService, SearchRefreshLockService>();
            services.AddSingleton<ISearchRefreshCompletionSignal, SearchRefreshCompletionSignal>();

            services.AddSingleton<ICacheService, RedisCacheService>();

            services.AddSingleton<RedisRateLimitCounterStore>();
            services.AddSingleton<IRateLimitCounterStore, CompositeRateLimitCounterStore>();

        }

        else

        {

            services.AddDistributedMemoryCache();

            services.AddSingleton<LocalSearchRefreshSingleFlightGate>();
            services.AddSingleton<LocalSearchRefreshCompletionRegistry>();
            services.AddSingleton<SearchRefreshLockDiagnostics>();
            services.AddSingleton<ISearchRefreshLockDiagnostics>(provider =>
                provider.GetRequiredService<SearchRefreshLockDiagnostics>());
            services.AddSingleton<ISearchRefreshLockService, SearchRefreshLockService>();
            services.AddSingleton<ISearchRefreshCompletionSignal, SearchRefreshCompletionSignal>();

            services.AddSingleton<ICacheService, RedisCacheService>();

            services.AddSingleton<IRateLimitCounterStore>(provider =>
                provider.GetRequiredService<InMemoryRateLimitCounterStore>());

        }



        var healthChecksBuilder = services.AddHealthChecks();



        if (!string.IsNullOrWhiteSpace(postgreSqlConnectionString))

        {

            healthChecksBuilder.AddNpgSql(postgreSqlConnectionString, name: "postgresql");

        }



        if (redisConfigurationOptions is not null)

        {

            healthChecksBuilder.AddRedis(redisConfigurationOptions.ToString(), name: "redis");

        }



        return services;

    }



    private static IEmailSender ResolveEmailSender(IServiceProvider serviceProvider)

    {

        var environment = serviceProvider.GetRequiredService<IHostEnvironment>();

        if (environment.IsEnvironment("Testing"))

        {

            return serviceProvider.GetRequiredService<CapturingEmailSender>();

        }



        var passwordResetOptions = serviceProvider.GetRequiredService<IOptions<PasswordResetOptions>>().Value;

        var provider = passwordResetOptions.EmailProvider?.Trim() ?? string.Empty;



        if (environment.IsDevelopment() &&

            provider.Equals("Development", StringComparison.OrdinalIgnoreCase))

        {

            return serviceProvider.GetRequiredService<DevelopmentEmailSender>();

        }



        if (provider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))

        {

            return serviceProvider.GetRequiredService<SmtpEmailSender>();

        }



        throw new InvalidOperationException(

            $"Unsupported password reset email provider '{provider}'. " +

            "Use 'Development' in development or 'Smtp' with configured SMTP settings in production.");

    }

}


