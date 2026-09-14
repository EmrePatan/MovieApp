using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.TvShowFollows;

namespace MovieApp.Application.Validation;

public static class TvShowFollowValidator
{
    public static void ValidatePreferencesUpdate(TvShowFollowPreferencesUpdate update)
    {
        if (update.NotifyNewSeasons is null && update.NotifyNewEpisodes is null)
        {
            throw new ValidationException(
                "At least one follow preference must be provided when updating an existing follow.");
        }
    }
}
