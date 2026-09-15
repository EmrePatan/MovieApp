using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

internal static class TmdbPersonMapper
{
    public static PersonProviderDetails ToPersonProviderDetails(
        TmdbPersonJson person,
        TmdbCombinedCreditsResponseJson combinedCredits) =>
        new(
            person.Id,
            person.Name.Trim(),
            TmdbMovieMapper.NormalizeImagePath(person.ProfilePath),
            string.IsNullOrWhiteSpace(person.Biography) ? null : person.Biography.Trim(),
            TmdbMovieMapper.ParseReleaseDate(person.Birthday),
            TmdbMovieMapper.ParseReleaseDate(person.Deathday),
            string.IsNullOrWhiteSpace(person.PlaceOfBirth) ? null : person.PlaceOfBirth.Trim(),
            string.IsNullOrWhiteSpace(person.KnownForDepartment) ? null : person.KnownForDepartment.Trim(),
            combinedCredits.Cast
                .Select(ToFilmographyCredit)
                .Where(credit => credit is not null)
                .Cast<PersonFilmographyCredit>()
                .ToList());

    private static PersonFilmographyCredit? ToFilmographyCredit(TmdbCombinedCreditCastJson credit)
    {
        if (credit.Id <= 0 || string.IsNullOrWhiteSpace(credit.Character))
        {
            return null;
        }

        var mediaType = credit.MediaType?.Trim().ToLowerInvariant();
        if (mediaType is not ("movie" or "tv"))
        {
            return null;
        }

        var title = mediaType == "movie"
            ? credit.Title?.Trim()
            : credit.Name?.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var releaseDate = mediaType == "movie"
            ? TmdbMovieMapper.ParseReleaseDate(credit.ReleaseDate)
            : TmdbMovieMapper.ParseReleaseDate(credit.FirstAirDate);

        return new PersonFilmographyCredit(
            mediaType,
            credit.Id,
            title,
            TmdbMovieMapper.NormalizeImagePath(credit.PosterPath),
            credit.Character.Trim(),
            releaseDate,
            Convert.ToDecimal(credit.Popularity),
            Convert.ToDecimal(credit.VoteAverage));
    }
}
