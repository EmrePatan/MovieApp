namespace MovieApp.Application.Models.AiRecommendations;

public sealed record PersonFilmographyConstraint(
    int PersonTmdbId,
    string PersonName,
    IReadOnlySet<PersonFilmographyCreditKey> CreditKeys)
{
    public bool Contains(ResolvedMovieIdentity resolved)
    {
        if (resolved.TmdbId is not int tmdbId || tmdbId <= 0)
        {
            return false;
        }

        var mediaType = string.Equals(resolved.MediaType, "tv", StringComparison.OrdinalIgnoreCase)
            ? "tv"
            : "movie";

        return CreditKeys.Contains(new PersonFilmographyCreditKey(mediaType, tmdbId));
    }
}

public readonly record struct PersonFilmographyCreditKey(string MediaType, int TmdbId);
