using System.Globalization;
using MovieApp.Application.Models.People;
using MovieApp.Contracts.People;

namespace MovieApp.Api.Mapping;

internal static class PersonContractMapper
{
    internal static PersonDetailResponse ToResponse(PersonDetailResult result) =>
        new(
            result.Id,
            result.TmdbId,
            result.Name,
            result.ProfileImagePath,
            string.IsNullOrWhiteSpace(result.Biography) ? null : result.Biography.Trim(),
            result.Birthday?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            result.Deathday?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            result.PlaceOfBirth,
            result.KnownForDepartment,
            result.Filmography.Select(ToFilmographyEntry).ToList());

    private static PersonFilmographyEntryResponse ToFilmographyEntry(PersonFilmographyEntryResult entry) =>
        new(
            entry.MediaType,
            entry.CatalogId,
            entry.TmdbId,
            entry.Title,
            entry.PosterPath,
            entry.Character,
            entry.ReleaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
}
