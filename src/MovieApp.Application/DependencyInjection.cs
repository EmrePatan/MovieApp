using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Caching;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Favorites;
using MovieApp.Application.Services.Identity;
using MovieApp.Application.Services.Movies;
using MovieApp.Application.Services.People;
using MovieApp.Application.Services.Ratings;
using MovieApp.Application.Services.Recommendations;
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
using MovieApp.Application.Services.TvShowFollows;
using MovieApp.Application.Services.MovieFollows;
using MovieApp.Application.Services.CatalogFollows;
using MovieApp.Application.Services.MovieRelease;
using MovieApp.Application.Services.Watchlists;
using MovieApp.Application.Services.Collections;
using MovieApp.Application.Services.Notifications;

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
        services.AddScoped<IGetPersonByTmdbIdService, GetPersonByTmdbIdService>();
        services.AddScoped<IGetCollectionService, GetCollectionService>();

        services.AddScoped<ISearchTvShowsService, SearchTvShowsService>();
        services.AddScoped<IGetTvShowByIdService, GetTvShowByIdService>();
        services.AddScoped<IGetTvShowByTmdbIdService, GetTvShowByTmdbIdService>();
        services.AddScoped<ITvShowSeasonSummaryHydrator, TvShowSeasonSummaryHydrator>();
        services.AddScoped<ITvShowCatalogSyncStateService, TvShowCatalogSyncStateService>();
        services.AddScoped<IGetTvShowCreditsService, GetTvShowCreditsService>();
        services.AddScoped<IGetTvShowWatchProvidersService, GetTvShowWatchProvidersService>();
        services.AddScoped<IGetTvShowVideosService, GetTvShowVideosService>();
        services.AddScoped<IGetSeasonService, GetSeasonService>();
        services.AddScoped<IGetEpisodeService, GetEpisodeService>();

        services.AddScoped<IRegisterUserService, RegisterUserService>();
        services.AddScoped<ILoginUserService, LoginUserService>();
        services.AddScoped<IGetCurrentUserService, GetCurrentUserService>();
        services.AddScoped<IProfileStatisticsCache, ProfileStatisticsCache>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IForgotPasswordService, ForgotPasswordService>();
        services.AddScoped<IResetPasswordService, ResetPasswordService>();

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

        services.AddScoped<IReleaseDetector, ReleaseDetector>();
        services.AddScoped<IReleaseNotificationFanoutService, ReleaseNotificationFanoutService>();
        services.AddScoped<IMovieReleaseFollowCleanupService, MovieReleaseFollowCleanupService>();
        services.AddScoped<IMovieReleaseCheckService, MovieReleaseCheckService>();

        services.AddScoped<IRegisterPushDeviceService, RegisterPushDeviceService>();
        services.AddScoped<IUnregisterPushDeviceService, UnregisterPushDeviceService>();

        services.AddScoped<IPushNotificationDeliveryPreparationService, PushNotificationDeliveryPreparationService>();
        services.AddScoped<IPushNotificationDispatchService, PushNotificationDispatchService>();
        services.AddScoped<IPushNotificationReceiptService, PushNotificationReceiptService>();

        services.AddScoped<ITmdbTvChangesSyncService, TmdbTvChangesSyncService>();
        services.AddScoped<ITvShowChangesTargetedRefreshService, TvShowChangesTargetedRefreshService>();

        services.AddScoped<IHotReleaseCheckService, HotReleaseCheckService>();
        services.AddScoped<IHotReleaseCandidateProcessor, HotReleaseCandidateProcessor>();

        services.AddScoped<ICreateWatchlistService, CreateWatchlistService>();
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
        services.AddScoped<IGenreService, GenreService>();
        services.AddScoped<ISearchHistoryService, SearchHistoryService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IHomeGlobalSectionsProvider, HomeGlobalSectionsProvider>();
        services.AddScoped<IHomeService, HomeService>();

        services.AddScoped<IGetNotificationsService, GetNotificationsService>();
        services.AddScoped<IGetUnreadNotificationCountService, GetUnreadNotificationCountService>();
        services.AddScoped<IMarkNotificationReadService, MarkNotificationReadService>();
        services.AddScoped<IMarkAllNotificationsReadService, MarkAllNotificationsReadService>();

        return services;
    }
}
