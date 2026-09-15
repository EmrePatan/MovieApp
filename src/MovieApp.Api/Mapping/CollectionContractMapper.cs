using System.Globalization;
using MovieApp.Application.Models.Collections;
using MovieApp.Contracts.Collections;

namespace MovieApp.Api.Mapping;

internal static class CollectionContractMapper
{
    internal static CollectionResponse ToResponse(CollectionDetailResult result) =>
        new(
            result.TmdbId,
            result.Name,
            result.Overview,
            result.PosterPath,
            result.BackdropPath,
            result.Parts.Select(ToPartResponse).ToList());

    private static CollectionPartResponse ToPartResponse(CollectionPartResult part) =>
        new(
            part.Id,
            part.TmdbId,
            part.Title,
            part.OriginalTitle,
            part.Overview,
            part.PosterPath,
            part.BackdropPath,
            part.ReleaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            part.VoteAverage,
            part.VoteCount);
}
