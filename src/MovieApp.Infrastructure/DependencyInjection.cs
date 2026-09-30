using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Keywords;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Infrastructure.Caching;
using MovieApp.Infrastructure.Configuration;
using MovieApp.Infrastructure.Health;
using MovieApp.Infrastructure.Email;
using MovieApp.Infrastructure.Identity;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Recommendations;
using MovieApp.Infrastructure.Keywords;
using MovieApp.Infrastructure.Persistence.Keywords;
using MovieApp.Infrastructure.Persistence.Repositories;
using MovieApp.Application.Abstractions.RateLimiting;
using MovieApp.Infrastructure.PushNotifications;
using MovieApp.Infrastructure.Providers;
using MovieApp.Infrastructure.Providers.MdbList;
using MovieApp.Infrastructure.AiRecommendations;
using MovieApp.Infrastructure.Providers.AzureTranslator;
using MovieApp.Infrastructure.Performance;
using MovieApp.Infrastructure.RateLimiting;
using MovieApp.Infrastructure.Storage;
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

        services.Configure<Application.Configuration.RefreshTokenOptions>(
            configuration.GetSection(Application.Configuration.RefreshTokenOptions.SectionName));

        services.AddOptions<SocialAuthOptions>()
            .Bind(configuration.GetSection(SocialAuthOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<SocialAuthOptions>, SocialAuthOptionsValidator>();

        services.Configure<RecommendationOptions>(configuration.GetSection(RecommendationOptions.SectionName));

        services.AddOptions<KeywordCatalogStatisticsOptions>()
            .Bind(configuration.GetSection(KeywordCatalogStatisticsOptions.SectionName));

        services.AddSingleton<KeywordCatalogStatisticsProvider>();
        services.AddSingleton<IKeywordCatalogStatisticsProvider>(provider =>
            provider.GetRequiredService<KeywordCatalogStatisticsProvider>());
        services.AddSingleton<IKeywordCatalogStatisticsLoader, KeywordCatalogStatisticsLoader>();
        services.AddSingleton<IKeywordCatalogStatisticsRefreshService, KeywordCatalogStatisticsRefreshService>();
        services.AddHostedService<KeywordCatalogStatisticsWarmupHostedService>();

        services.Configure<HomeOptions>(configuration.GetSection(HomeOptions.SectionName));

        services.Configure<InsightsOptions>(configuration.GetSection(InsightsOptions.SectionName));

        services.AddOptions<TopRatedOptions>()
            .Bind(configuration.GetSection(TopRatedOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<TopRatedOptions>, TopRatedOptionsValidator>();

        services.AddOptions<AdvancedDiscoverOptions>()
            .Bind(configuration.GetSection(AdvancedDiscoverOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AdvancedDiscoverOptions>, AdvancedDiscoverOptionsValidator>();

        services.AddOptions<NewReleasesOptions>()
            .Bind(configuration.GetSection(NewReleasesOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<NewReleasesOptions>, NewReleasesOptionsValidator>();

        services.AddOptions<NowInTheatersOptions>()
            .Bind(configuration.GetSection(NowInTheatersOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<NowInTheatersOptions>, NowInTheatersOptionsValidator>();

        services.Configure<PushNotificationsOptions>(configuration.GetSection(PushNotificationsOptions.SectionName));

        services.Configure<NotificationRetentionOptions>(
            configuration.GetSection(NotificationRetentionOptions.SectionName));

        services.AddOptions<SearchOptions>()
            .Bind(configuration.GetSection(SearchOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<SearchOptions>, SearchOptionsValidator>();

        services.AddOptions<CatalogKeywordBackfillOptions>()
            .Bind(configuration.GetSection(CatalogKeywordBackfillOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<MdbListKeywordBackfillOptions>()
            .Bind(configuration.GetSection(MdbListKeywordBackfillOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<KeywordGraphOptions>()
            .Bind(configuration.GetSection(KeywordGraphOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<KeywordGraphOptions>, KeywordGraphOptionsValidator>();
        services.AddSingleton<IValidateOptions<CatalogKeywordBackfillOptions>, CatalogKeywordBackfillOptionsValidator>();
        services.AddSingleton<IValidateOptions<MdbListKeywordBackfillOptions>, MdbListKeywordBackfillOptionsValidator>();
        services.AddSingleton<IValidateOptions<CatalogGenreBackfillOptions>, CatalogGenreBackfillOptionsValidator>();

        services.AddOptions<TvUpcomingEpisodeSyncOptions>()
            .Bind(configuration.GetSection(TvUpcomingEpisodeSyncOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<TvUpcomingEpisodeSyncOptions>, TvUpcomingEpisodeSyncOptionsValidator>();

        services.Configure<HotThisWeekTrendingRefreshOptions>(
            configuration.GetSection(HotThisWeekTrendingRefreshOptions.SectionName));

        services.AddOptions<ReleaseRegionOptions>()
            .Bind(configuration.GetSection(ReleaseRegionOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ReleaseRegionOptions>, ReleaseRegionOptionsValidator>();

        services.Configure<SharedResendEmailOptions>(configuration.GetSection(SharedResendEmailOptions.SectionName));

        services.AddOptions<PasswordResetOptions>()

            .Bind(configuration.GetSection(PasswordResetOptions.SectionName))

            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<PasswordResetOptions>, PasswordResetOptionsValidator>();

        services.AddOptions<ResendPasswordResetEmailOptions>()
            .Bind(configuration.GetSection(ResendPasswordResetEmailOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ResendPasswordResetEmailOptions>, ResendPasswordResetEmailOptionsValidator>();

        services.AddOptions<EmailVerificationOptions>()
            .Bind(configuration.GetSection(EmailVerificationOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<EmailVerificationOptions>, EmailVerificationOptionsValidator>();

        services.AddOptions<ResendVerificationEmailOptions>()
            .Bind(configuration.GetSection(ResendVerificationEmailOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<ResendVerificationEmailOptions>, ResendVerificationEmailOptionsValidator>();

        services.AddAiRecommendations(configuration);
        services.AddReviewTranslation(configuration);
        services.AddKeywordTranslation(configuration);

        services.AddMovieDataProviders(configuration);

        services.AddTvShowDataProviders(configuration);

        services.AddTrendingWeekDataProviders(configuration);

        services.AddDetailProviders(configuration);

        services.AddExternalRatingsProviders(configuration);



        var postgreSqlOptions = configuration

            .GetSection(PostgreSqlOptions.SectionName)

            .Get<PostgreSqlOptions>() ?? new PostgreSqlOptions();



        var redisOptions = configuration

            .GetSection(RedisOptions.SectionName)

            .Get<RedisOptions>() ?? new RedisOptions();



        var postgreSqlConnectionString = postgreSqlOptions.IsConfigured()

            ? postgreSqlOptions.ResolveConnectionString()

            : string.Empty;



        services.AddSingleton<HomeColdPerfDbTelemetryInterceptor>();
        services.AddSingleton<HomeColdPerfConnectionInterceptor>();

        void ConfigureApplicationDbContext(IServiceProvider serviceProvider, DbContextOptionsBuilder options) =>
            options.UseNpgsql(
                    postgreSqlConnectionString,
                    npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3))
                .AddInterceptors(
                    serviceProvider.GetRequiredService<HomeColdPerfDbTelemetryInterceptor>(),
                    serviceProvider.GetRequiredService<HomeColdPerfConnectionInterceptor>());

        services.AddDbContextFactory<ApplicationDbContext>(ConfigureApplicationDbContext);

        services.AddScoped(provider =>
            provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());



        services.AddScoped<IContentSearchTitleSynchronizer, ContentSearchTitleSynchronizer>();
        services.AddScoped<IContentLocalizedPosterRepository, ContentLocalizedPosterRepository>();
        services.AddScoped<IContentLocalizedPosterBackfillRepository, ContentLocalizedPosterBackfillRepository>();
        services.AddScoped<IKeywordLocalizationBackfillRepository, KeywordLocalizationBackfillRepository>();
        services.AddScoped<IKeywordLocalizationCoverageRepository, KeywordLocalizationCoverageRepository>();
        services.AddScoped<IContentSearchTitleCatalogBackfillService, ContentSearchTitleCatalogBackfillService>();
        services.AddScoped<IContentSearchTitleProviderEnrichmentRepository, ContentSearchTitleProviderEnrichmentRepository>();
        services.AddScoped<IMovieRepository, MovieRepository>();

        services.AddScoped<IMovieRegionalReleaseRepository, MovieRegionalReleaseRepository>();

        services.AddScoped<IKeywordCatalogRepository, KeywordCatalogRepository>();
        services.AddScoped<IKeywordDiscoverReadRepository, KeywordDiscoverReadRepository>();
        services.AddScoped<IKeywordGraphReconciliationService, KeywordGraphReconciliationService>();

        services.AddScoped<ICatalogKeywordBackfillRepository, CatalogKeywordBackfillRepository>();
        services.AddScoped<IMdbListKeywordBackfillRepository, MdbListKeywordBackfillRepository>();
        services.AddScoped<ICatalogGenreBackfillRepository, CatalogGenreBackfillRepository>();

        services.AddScoped<IPersonRepository, PersonRepository>();

        services.AddScoped<ITvShowRepository, TvShowRepository>();

        services.AddScoped<ISeasonRepository, SeasonRepository>();

        services.AddScoped<IEpisodeRepository, EpisodeRepository>();
        services.AddScoped<ITvWatchStatePreparationRepository, TvWatchStatePreparationRepository>();

        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<IUserExternalLoginRepository, UserExternalLoginRepository>();

        services.AddScoped<IUserAvatarReadRepository, UserAvatarReadRepository>();
        services.AddScoped<IUserAvatarImageProcessor, UserAvatarImageProcessor>();
        services.AddUserAvatarStorage(configuration);

        services.AddMemoryCache();

        services.AddSingleton<SecurityStampCache>();

        services.AddHttpClient(nameof(AppleJwksProvider));

        services.AddSingleton<AppleJwksProvider>();

        services.AddScoped<ISocialIdentityTokenVerifier, GoogleIdTokenVerifier>();

        services.AddScoped<ISocialIdentityTokenVerifier, AppleIdTokenVerifier>();

        services.AddScoped<IUserStatisticsRepository, UserStatisticsRepository>();
        services.AddScoped<IInsightsRepository, InsightsRepository>();

        services.AddScoped<IFavoriteRepository, FavoriteRepository>();

        services.AddScoped<ICatalogFollowRepository, CatalogFollowRepository>();

        services.AddScoped<ITvShowFollowRepository, TvShowFollowRepository>();

        services.AddScoped<ICatalogFollowCatalogRepository, CatalogFollowCatalogRepository>();
        services.AddScoped<ITvUpcomingEpisodeSyncRepository, TvUpcomingEpisodeSyncRepository>();

        services.AddScoped<ITvShowCatalogSyncStateRepository, TvShowCatalogSyncStateRepository>();

        services.AddScoped<ITmdbTvChangesSyncCheckpointRepository, TmdbTvChangesSyncCheckpointRepository>();

        services.AddScoped<IFollowedTvShowCatalogRepository, FollowedTvShowCatalogRepository>();

        services.AddScoped<ICatalogChangesRelevanceRepository, CatalogChangesRelevanceRepository>();

        services.AddScoped<IHotReleaseCandidateRepository, HotReleaseCandidateRepository>();

        services.AddScoped<ICatalogReleaseEventRepository, CatalogReleaseEventRepository>();

        services.AddScoped<IReleaseNotificationFanoutRepository, ReleaseNotificationFanoutRepository>();

        services.AddScoped<IPushDeviceRepository, PushDeviceRepository>();

        services.AddScoped<IPushNotificationDeliveryRepository, PushNotificationDeliveryRepository>();
        services.AddScoped<IProductMetricDailyRepository, ProductMetricDailyRepository>();
        services.AddScoped<IUserReleaseNotificationRepository, UserReleaseNotificationRepository>();

        services.AddExpoPushClient();

        services.AddScoped<IReleaseDetectionCatalogRepository, ReleaseDetectionCatalogRepository>();

        services.AddScoped<IWatchlistRepository, WatchlistRepository>();

        services.AddScoped<IWatchlistItemRepository, WatchlistItemRepository>();
        services.AddScoped<IUserWatchlistShareRepository, UserWatchlistShareRepository>();

        services.AddScoped<IRatingRepository, RatingRepository>();

        services.AddScoped<IReviewRepository, ReviewRepository>();

        services.AddScoped<IWatchedMovieRepository, WatchedMovieRepository>();

        services.AddScoped<IWatchedEpisodeRepository, WatchedEpisodeRepository>();

        services.AddScoped<ILibraryRepository, LibraryRepository>();
        services.AddScoped<ILibraryActionStatusRepository, LibraryActionStatusRepository>();

        services.AddScoped<ISearchRepository, SearchRepository>();

        services.AddScoped<IGenreReadRepository, GenreReadRepository>();

        services.AddScoped<ISearchHistoryRepository, SearchHistoryRepository>();

        services.AddScoped<ISearchProviderRefreshRepository, SearchProviderRefreshRepository>();

        services.AddScoped<IExternalRatingSnapshotRepository, ExternalRatingSnapshotRepository>();

        services.AddScoped<IRecommendationRepository, RecommendationRepository>();

        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();

        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();

        services.AddSingleton<InMemoryRateLimitCounterStore>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        services.AddScoped<ITokenService, JwtTokenService>();

        services.AddSingleton<CapturingEmailSender>();

        services.AddSingleton<DevelopmentEmailSender>();

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
            healthChecksBuilder.AddCheck<PendingDatabaseMigrationsHealthCheck>(
                "database-migrations",
                failureStatus: HealthStatus.Unhealthy);

        }



        if (redisConfigurationOptions is not null)

        {

            healthChecksBuilder.AddRedis(redisConfigurationOptions.ToString(), name: "redis");

        }



        services.AddSingleton<IDevelopmentDatabaseMigrator, EfCoreDevelopmentDatabaseMigrator>();

        services.AddHostedService<DevelopmentDatabaseMigrationHostedService>();



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



        if (provider.Equals("Resend", StringComparison.OrdinalIgnoreCase))

        {

            throw new InvalidOperationException(

                "Password reset email delivery is handled by the Resend delivery pipeline. " +

                "ForgotPasswordService does not use IEmailSender.");

        }



        throw new InvalidOperationException(

            $"Unsupported password reset email provider '{provider}'. " +

            "Use 'Development' in development or 'Resend' with configured Resend settings in production.");

    }

}


