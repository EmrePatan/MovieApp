using MovieApp.Application.Models.TvShowFollows;

namespace MovieApp.Application.Validation;

public static class TvShowFollowValidator
{
    public static void ValidatePreferencesUpdate(TvShowFollowPreferencesUpdate update) =>
        CatalogFollowValidator.ValidateTvPreferencesUpdate(update);
}
