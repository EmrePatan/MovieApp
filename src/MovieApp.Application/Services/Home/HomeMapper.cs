using MovieApp.Application.Models.CatalogFollows;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Models.WatchHistory;

namespace MovieApp.Application.Services.Home;

internal static class HomeMapper
{
    public static HomeItem FromSearchItem(SearchItem item) =>
        new(
            item.Id,
            item.Type,
            item.Title,
            item.OriginalTitle,
            item.PosterUrl,
            item.BackdropUrl,
            item.ReleaseDate,
            item.VoteAverage,
            item.VoteCount);

    public static HomeItem FromRecommendationItem(RecommendationItem item) =>
        new(
            item.Id,
            item.Type,
            item.Title,
            item.OriginalTitle,
            item.PosterUrl,
            item.BackdropUrl,
            item.ReleaseDate,
            item.VoteAverage,
            item.VoteCount);

    public static HomeItem FromContinueWatchingItem(ContinueWatchingItemResult item) =>
        new(
            item.TvShowId,
            "tv",
            item.Title,
            item.OriginalTitle,
            item.PosterUrl,
            item.BackdropUrl,
            item.FirstAirDate,
            item.VoteAverage,
            item.VoteCount);

    public static HomeItem FromUpcomingItem(CatalogUpcomingItemResult item) =>
        new(
            item.ContentId,
            item.ContentType == Domain.Enums.CatalogContentType.Movie ? "movie" : "tv",
            item.Title,
            null,
            item.PosterPath,
            null,
            item.ReleaseDate,
            0m,
            0,
            item.UpcomingKind.ToString(),
            item.EpisodeId,
            item.SeasonNumber,
            item.EpisodeNumber,
            item.EpisodeName);
}
