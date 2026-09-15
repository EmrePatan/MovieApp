using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

internal static class TmdbTrendingMapper
{
    internal static TrendingWeekProviderItem? ToProviderItem(TmdbTrendingResultJson result)
    {
        var mediaType = result.MediaType?.Trim().ToLowerInvariant();
        if (mediaType is not ("movie" or "tv"))
        {
            return null;
        }

        var title = mediaType == "movie"
            ? result.Title
            : result.Name;

        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var originalTitle = mediaType == "movie"
            ? result.OriginalTitle
            : result.OriginalName;

        var releaseDate = mediaType == "movie"
            ? TmdbMovieMapper.ParseReleaseDate(result.ReleaseDate)
            : TmdbMovieMapper.ParseReleaseDate(result.FirstAirDate);

        return new TrendingWeekProviderItem(
            mediaType,
            result.Id,
            title,
            originalTitle,
            result.Overview,
            releaseDate,
            TmdbMovieMapper.NormalizeImagePath(result.PosterPath),
            TmdbMovieMapper.NormalizeImagePath(result.BackdropPath),
            result.VoteAverage,
            result.VoteCount);
    }
}
