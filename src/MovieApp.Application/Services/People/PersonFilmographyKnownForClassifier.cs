using MovieApp.Application.Models.People;
using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Services.People;

internal static class PersonFilmographyKnownForClassifier
{
    private const int AnimationGenreId = 16;
    private const int DocumentaryGenreId = 99;
    private const int TvMovieGenreId = 10770;
    private const int KidsGenreId = 10762;
    private const int NewsGenreId = 10763;
    private const int RealityGenreId = 10764;
    private const int SoapGenreId = 10766;
    private const int TalkGenreId = 10767;

    internal static string Classify(PersonFilmographyCredit credit)
    {
        if (credit.MediaType == "movie")
        {
            return ClassifyMovie(credit.GenreTmdbIds);
        }

        if (credit.MediaType == "tv")
        {
            return ClassifyTelevision(credit.GenreTmdbIds, credit.TvShowType);
        }

        return PersonFilmographyKnownForCategories.OtherTelevision;
    }

    private static string ClassifyMovie(IReadOnlyList<int>? genreIds)
    {
        if (ContainsGenre(genreIds, TvMovieGenreId))
        {
            return PersonFilmographyKnownForCategories.TelevisionMovie;
        }

        if (ContainsGenre(genreIds, AnimationGenreId))
        {
            return PersonFilmographyKnownForCategories.Animation;
        }

        if (ContainsGenre(genreIds, DocumentaryGenreId))
        {
            return PersonFilmographyKnownForCategories.Documentary;
        }

        return PersonFilmographyKnownForCategories.Movie;
    }

    private static string ClassifyTelevision(IReadOnlyList<int>? genreIds, string? tvShowType)
    {
        if (IsMiniSeries(tvShowType))
        {
            return PersonFilmographyKnownForCategories.MiniSeries;
        }

        if (ContainsGenre(genreIds, TalkGenreId)
            || ContainsGenre(genreIds, RealityGenreId)
            || ContainsGenre(genreIds, NewsGenreId))
        {
            return PersonFilmographyKnownForCategories.TalkVarietyReality;
        }

        if (ContainsGenre(genreIds, DocumentaryGenreId))
        {
            return PersonFilmographyKnownForCategories.Documentary;
        }

        if (ContainsGenre(genreIds, AnimationGenreId))
        {
            return PersonFilmographyKnownForCategories.Animation;
        }

        if (ContainsGenre(genreIds, SoapGenreId) || ContainsGenre(genreIds, KidsGenreId))
        {
            return PersonFilmographyKnownForCategories.OtherTelevision;
        }

        return PersonFilmographyKnownForCategories.ScriptedTelevision;
    }

    private static bool IsMiniSeries(string? tvShowType)
    {
        if (string.IsNullOrWhiteSpace(tvShowType))
        {
            return false;
        }

        return tvShowType.Trim().Equals("Miniseries", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsGenre(IReadOnlyList<int>? genreIds, int genreId) =>
        genreIds is not null && genreIds.Contains(genreId);
}
