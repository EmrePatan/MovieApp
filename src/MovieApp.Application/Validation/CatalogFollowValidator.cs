using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.TvShowFollows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Validation;

public static class CatalogFollowValidator
{
    public static void ValidateTvPreferencesUpdate(TvShowFollowPreferencesUpdate update)
    {
        if (update.NotifyNewSeasons is null && update.NotifyNewEpisodes is null)
        {
            throw new ValidationException(
                "At least one follow preference must be provided when updating an existing follow.");
        }
    }

    public static void ValidateTvFollow(CatalogFollow follow)
    {
        if (follow.ContentType != CatalogContentType.Tv)
        {
            throw new ValidationException("Catalog follow is not a TV show follow.");
        }

        if (follow.NotifyMovieRelease)
        {
            throw new ValidationException("TV show follows cannot enable movie release notifications.");
        }

        if (!follow.NotifyNewSeasons && !follow.NotifyNewEpisodes)
        {
            throw new ValidationException(
                "At least one of notify new seasons or notify new episodes must be enabled.");
        }
    }

    public static void ValidateMovieFollow(CatalogFollow follow)
    {
        if (follow.ContentType != CatalogContentType.Movie)
        {
            throw new ValidationException("Catalog follow is not a movie follow.");
        }

        if (!follow.NotifyMovieRelease)
        {
            throw new ValidationException("Movie follows must enable movie release notifications.");
        }

        if (follow.NotifyNewSeasons || follow.NotifyNewEpisodes)
        {
            throw new ValidationException("Movie follows cannot enable TV season or episode notifications.");
        }
    }

    public static void ValidateMovieFollowEligibility(DateOnly? releaseDate, DateOnly today)
    {
        if (releaseDate is null)
        {
            throw new ValidationException("Movie follows require a known release date.");
        }

        if (releaseDate.Value <= today)
        {
            throw new ValidationException("Movie follows are only available for unreleased movies.");
        }
    }
}
