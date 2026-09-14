using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Favorites;
using MovieApp.Application.Services.Identity;
using MovieApp.Application.Services.Movies;
using MovieApp.Application.Services.Ratings;
using MovieApp.Application.Services.Recommendations;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Services.Reviews;
using MovieApp.Application.Services.TvShows;
using MovieApp.Application.Services.WatchHistory;
using MovieApp.Application.Services.Watchlists;

namespace MovieApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ISearchMoviesService, SearchMoviesService>();
        services.AddScoped<IGetMovieByIdService, GetMovieByIdService>();
        services.AddScoped<IGetMovieCreditsService, GetMovieCreditsService>();
        services.AddScoped<IGetMovieWatchProvidersService, GetMovieWatchProvidersService>();

        services.AddScoped<ISearchTvShowsService, SearchTvShowsService>();
        services.AddScoped<IGetTvShowByIdService, GetTvShowByIdService>();
        services.AddScoped<ITvShowSeasonSummaryHydrator, TvShowSeasonSummaryHydrator>();
        services.AddScoped<IGetTvShowCreditsService, GetTvShowCreditsService>();
        services.AddScoped<IGetTvShowWatchProvidersService, GetTvShowWatchProvidersService>();
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
        services.AddScoped<ISearchHistoryService, SearchHistoryService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IHomeGlobalSectionsProvider, HomeGlobalSectionsProvider>();
        services.AddScoped<IHomeService, HomeService>();

        return services;
    }
}
