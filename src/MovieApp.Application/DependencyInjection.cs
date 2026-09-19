using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Caching;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Library;
using MovieApp.Application.Services.Favorites;
using MovieApp.Application.Services.Identity;
using MovieApp.Application.Services.Movies;
using MovieApp.Application.Services.People;
using MovieApp.Application.Services.Ratings;
using MovieApp.Application.Services.Recommendations;
using MovieApp.Application.Services.Discovery;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Services.Reviews;
using MovieApp.Application.Services.TvShows;
using MovieApp.Application.Services.WatchHistory;
using MovieApp.Application.Abstractions.ReleaseDetection;
using MovieApp.Application.Services.ReleaseDetection;
using MovieApp.Application.Services.PushDevices;
using MovieApp.Application.Services.PushNotifications;
using MovieApp.Application.Services.ReleaseNotifications;
using MovieApp.Application.Services.HotRelease;
using MovieApp.Application.Services.TvShowChanges;
using MovieApp.Application.Services.MovieChanges;
using MovieApp.Application.Services.TvShowFollows;
using MovieApp.Application.Services.MovieFollows;
using MovieApp.Application.Services.CatalogFollows;
using MovieApp.Application.Services.MovieRelease;
using MovieApp.Application.Services.Watchlists;
using MovieApp.Application.Services.Collections;
using MovieApp.Application.Services.Notifications;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.RegionalRelease;
using MovieApp.Application.Services.TvUpcomingEpisodes;
using MovieApp.Application.Services.ProductMetrics;
using MovieApp.Application.Services.Insights;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ISearchMoviesService, SearchMoviesService>();
        services.AddScoped<IGetMovieByIdService, GetMovieByIdService>();
        services.AddScoped<IGetMovieByTmdbIdService, GetMovieByTmdbIdService>();
        services.AddScoped<IGetMovieCreditsService, GetMovieCreditsService>();
        services.AddScoped<IGetMovieWatchProvidersService, GetMovieWatchProvidersService>();
        services.AddScoped<IGetMovieVideosService, GetMovieVideosService>();
        services.AddScoped<IGetMovieImagesService, GetMovieImagesService>();
        services.AddScoped<IGetPersonByTmdbIdService, GetPersonByTmdbIdService>();
        services.AddScoped<IGetPersonImagesService, GetPersonImagesService>();
        services.AddScoped<IGetCollectionService, GetCollectionService>();
        services.AddScoped<IDetailLocalizationOverlayService, DetailLocalizationOverlayService>();
        services.AddScoped<ISummaryLocalizationOverlayService, SummaryLocalizationOverlayService>();

        services.AddScoped<ISearchTvShowsService, SearchTvShowsService>();
        services.AddScoped<IGetTvShowByIdService, GetTvShowByIdService>();
        services.AddScoped<IGetTvShowByTmdbIdService, GetTvShowByTmdbIdService>();
        services.AddScoped<ITvShowSeasonSummaryHydrator, TvShowSeasonSummaryHydrator>();
        services.AddScoped<ITvShowCatalogSyncStateService, TvShowCatalogSyncStateService>();
        services.AddScoped<IGetTvShowCreditsService, GetTvShowCreditsService>();
        services.AddScoped<IGetTvShowWatchProvidersService, GetTvShowWatchProvidersService>();
        services.AddScoped<IGetTvShowVideosService, GetTvShowVideosService>();
        services.AddScoped<IGetTvShowImagesService, GetTvShowImagesService>();
        services.AddScoped<IGetSeasonService, GetSeasonService>();
        services.AddScoped<IGetEpisodeService, GetEpisodeService>();

        services.AddScoped<IRegisterUserService, RegisterUserService>();
        services.AddScoped<ILoginUserService, LoginUserService>();
        services.AddScoped<ISocialAuthService, SocialAuthService>();
        services.AddScoped<IGetCurrentUserService, GetCurrentUserService>();
        services.AddScoped<IProfileStatisticsCache, ProfileStatisticsCache>();
        services.AddScoped<IInsightsCache, InsightsCache>();
        services.AddScoped<IUserAnalyticsCacheInvalidator, UserAnalyticsCacheInvalidator>();
        services.AddScoped<IInsightsSummaryService, InsightsSummaryService>();
        services.AddScoped<IInsightsAnalyticsService, InsightsAnalyticsService>();
        services.AddScoped<IInsightsV3Service, InsightsV3Service>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IForgotPasswordService, ForgotPasswordService>();
        services.AddScoped<IResetPasswordService, ResetPasswordService>();
        services.AddScoped<IVerifyEmailService, VerifyEmailService>();
        services.AddScoped<IResendVerificationService, ResendVerificationService>();

        services.AddScoped<IAddMovieFavoriteService, AddMovieFavoriteService>();
        services.AddScoped<IRemoveMovieFavoriteService, RemoveMovieFavoriteService>();
        services.AddScoped<IAddTvShowFavoriteService, AddTvShowFavoriteService>();
        services.AddScoped<IRemoveTvShowFavoriteService, RemoveTvShowFavoriteService>();
        services.AddScoped<IGetFavoritesService, GetFavoritesService>();
        services.AddScoped<IGetFavoriteStatusService, GetFavoriteStatusService>();

        services.AddScoped<IGetTvShowFollowStatusService, GetTvShowFollowStatusService>();
        services.AddScoped<IUpsertTvShowFollowService, UpsertTvShowFollowService>();
        services.AddScoped<IRemoveTvShowFollowService, RemoveTvShowFollowService>();
        services.AddScoped<IGetTvShowFollowsService, GetTvShowFollowsService>();
        services.AddScoped<ITvShowFollowBaselineService, TvShowFollowBaselineService>();

        services.AddScoped<IGetMovieFollowStatusService, GetMovieFollowStatusService>();
        services.AddScoped<IUpsertMovieFollowService, UpsertMovieFollowService>();
        services.AddScoped<IRemoveMovieFollowService, RemoveMovieFollowService>();
        services.AddScoped<IGetCatalogFollowsService, GetCatalogFollowsService>();
        services.AddScoped<IGetCatalogUpcomingService, GetCatalogUpcomingService>();
        services.AddScoped<ITvUpcomingEpisodeSyncService, TvUpcomingEpisodeSyncService>();

        services.AddScoped<IReleaseDetector, ReleaseDetector>();
        services.AddScoped<IReleaseNotificationFanoutService, ReleaseNotificationFanoutService>();
        services.AddScoped<IMovieReleaseFollowCleanupService, MovieReleaseFollowCleanupService>();
        services.AddScoped<IRegionalEffectiveReleaseResolver, RegionalEffectiveReleaseResolver>();
        services.AddScoped<IMovieReleaseCheckService, MovieReleaseCheckService>();

        services.AddScoped<IRegisterPushDeviceService, RegisterPushDeviceService>();
        services.AddScoped<IUnregisterPushDeviceService, UnregisterPushDeviceService>();

        services.AddScoped<IPushNotificationDeliveryPreparationService, PushNotificationDeliveryPreparationService>();
        services.AddScoped<IPushNotificationDispatchService, PushNotificationDispatchService>();
        services.AddScoped<IPushNotificationReceiptService, PushNotificationReceiptService>();

        services.AddScoped<ITmdbTvChangesSyncService, TmdbTvChangesSyncService>();
        services.AddScoped<ITvShowChangesTargetedRefreshService, TvShowChangesTargetedRefreshService>();
        services.AddScoped<ITmdbMovieChangesSyncService, TmdbMovieChangesSyncService>();
        services.AddScoped<IMovieChangesTargetedRefreshService, MovieChangesTargetedRefreshService>();
        services.AddScoped<ITvShowCatalogDetailsCacheInvalidator, TvShowCatalogDetailsCacheInvalidator>();
        services.AddScoped<IMovieCatalogDetailsCacheInvalidator, MovieCatalogDetailsCacheInvalidator>();

        services.AddScoped<IHotReleaseCheckService, HotReleaseCheckService>();
        services.AddScoped<IHotReleaseCandidateProcessor, HotReleaseCandidateProcessor>();

        services.AddScoped<ICreateWatchlistService, CreateWatchlistService>();
        services.AddScoped<IUpdateWatchlistService, UpdateWatchlistService>();
        services.AddScoped<IDeleteWatchlistService, DeleteWatchlistService>();
        services.AddScoped<IGetWatchlistsService, GetWatchlistsService>();
        services.AddScoped<IGetWatchlistService, GetWatchlistService>();
        services.AddScoped<IAddMovieToWatchlistService, AddMovieToWatchlistService>();
        services.AddScoped<IRemoveMovieFromWatchlistService, RemoveMovieFromWatchlistService>();
        services.AddScoped<IAddTvShowToWatchlistService, AddTvShowToWatchlistService>();
        services.AddScoped<IRemoveTvShowFromWatchlistService, RemoveTvShowFromWatchlistService>();
        services.AddScoped<IGetWatchlistItemsService, GetWatchlistItemsService>();
        services.AddScoped<IGetWatchlistMembershipService, GetWatchlistMembershipService>();

        services.AddScoped<IRatingService, RatingService>();
        services.AddScoped<IReviewService, ReviewService>();

        services.AddScoped<IWatchHistoryService, WatchHistoryService>();

        services.AddScoped<IUnifiedSearchProviderIngestionService, UnifiedSearchProviderIngestionService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IAutocompleteService, AutocompleteService>();
        services.AddScoped<IDiscoveryService, DiscoveryService>();
        services.AddScoped<IDiscoverBrowseService, DiscoverBrowseService>();
        services.AddScoped<IAdvancedDiscoverService, AdvancedDiscoverService>();
        services.AddScoped<IDiscoveryWatchProvidersService, DiscoveryWatchProvidersService>();
        services.AddScoped<INowInTheatersService, NowInTheatersService>();
        services.AddScoped<IOnTvThisWeekService, OnTvThisWeekService>();
        services.AddScoped<IWorldCinemaService, WorldCinemaService>();
        services.AddScoped<IPickSomethingService, PickSomethingService>();
        services.AddScoped<IExplorePreviewService, ExplorePreviewService>();
        services.AddScoped<ICatalogKeywordIngestionService, CatalogKeywordIngestionService>();
        services.AddScoped<ICatalogKeywordBackfillItemProcessor, CatalogKeywordBackfillItemProcessor>();
        services.AddScoped<ICatalogKeywordBackfillService, CatalogKeywordBackfillService>();
        services.AddScoped<ICatalogProviderUpsertService, CatalogProviderUpsertService>();
        services.AddScoped<IGenreService, GenreService>();
        services.AddScoped<ISearchHistoryService, SearchHistoryService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IHomeGlobalSectionsProvider, HomeGlobalSectionsProvider>();
        services.AddScoped<IHotThisWeekTrendingSnapshotService, HotThisWeekTrendingSnapshotService>();
        services.AddScoped<IHotThisWeekService, HotThisWeekService>();
        services.AddScoped<IHomeTopRatedService, HomeTopRatedService>();
        services.AddScoped<IGetHomeComingUpService, GetHomeComingUpService>();
        services.AddScoped<IHomeService, HomeService>();

        services.AddScoped<IGetNotificationsService, GetNotificationsService>();
        services.AddScoped<IGetUnreadNotificationCountService, GetUnreadNotificationCountService>();
        services.AddScoped<IMarkNotificationReadService, MarkNotificationReadService>();
        services.AddScoped<IMarkAllNotificationsReadService, MarkAllNotificationsReadService>();
        services.AddScoped<IDeleteNotificationService, DeleteNotificationService>();
        services.AddScoped<INotificationInboxCleanupService, NotificationInboxCleanupService>();
        services.AddScoped<IIncrementProductMetricService, IncrementProductMetricService>();
        services.AddScoped<ILibraryService, LibraryService>();

        return services;
    }
}
