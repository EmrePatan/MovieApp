using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.People;
using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Services.People;

internal static class PersonFilmographyComposer
{
    internal static async Task<IReadOnlyList<PersonFilmographyEntryResult>> ComposeAsync(
        IReadOnlyList<PersonFilmographyCredit> credits,
        IMovieRepository movieRepository,
        ITvShowRepository tvShowRepository,
        CancellationToken cancellationToken)
    {
        var actingCredits = credits
            .Where(IsValidActingCredit)
            .GroupBy(credit => (credit.MediaType, credit.TmdbId))
            .Select(group => group
                .OrderByDescending(credit => credit.ReleaseDate ?? DateOnly.MinValue)
                .First())
            .ToList();

        var movieTmdbIds = actingCredits
            .Where(credit => credit.MediaType == "movie")
            .Select(credit => credit.TmdbId)
            .ToList();

        var tvTmdbIds = actingCredits
            .Where(credit => credit.MediaType == "tv")
            .Select(credit => credit.TmdbId)
            .ToList();

        var movieCatalogIds = movieTmdbIds.Count > 0
            ? await movieRepository.GetExistingIdsByTmdbIdsAsync(movieTmdbIds, cancellationToken)
            : new Dictionary<int, Guid>();

        var tvCatalogIds = tvTmdbIds.Count > 0
            ? await tvShowRepository.GetExistingIdsByTmdbIdsAsync(tvTmdbIds, cancellationToken)
            : new Dictionary<int, Guid>();

        var dated = actingCredits
            .Where(credit => credit.ReleaseDate.HasValue)
            .OrderByDescending(credit => credit.ReleaseDate);

        var undated = actingCredits
            .Where(credit => !credit.ReleaseDate.HasValue)
            .OrderBy(credit => credit.Title, StringComparer.OrdinalIgnoreCase);

        return dated
            .Concat(undated)
            .Select(credit => new PersonFilmographyEntryResult(
                credit.MediaType,
                ResolveCatalogId(credit, movieCatalogIds, tvCatalogIds),
                credit.TmdbId,
                credit.Title,
                credit.PosterPath,
                credit.Character,
                credit.ReleaseDate))
            .ToList();
    }

    private static Guid? ResolveCatalogId(
        PersonFilmographyCredit credit,
        IReadOnlyDictionary<int, Guid> movieCatalogIds,
        IReadOnlyDictionary<int, Guid> tvCatalogIds)
    {
        if (credit.MediaType == "movie")
        {
            return movieCatalogIds.TryGetValue(credit.TmdbId, out var catalogId)
                ? catalogId
                : null;
        }

        return tvCatalogIds.TryGetValue(credit.TmdbId, out var tvCatalogId)
            ? tvCatalogId
            : null;
    }

    private static bool IsValidActingCredit(PersonFilmographyCredit credit) =>
        credit.TmdbId > 0
        && !string.IsNullOrWhiteSpace(credit.Title)
        && (credit.MediaType == "movie" || credit.MediaType == "tv")
        && !string.IsNullOrWhiteSpace(credit.Character);
}
