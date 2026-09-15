using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

internal static class TmdbCollectionMapper
{
    internal static CollectionProviderDetails ToCollectionProviderDetails(TmdbCollectionResponseJson collection)
    {
        return new CollectionProviderDetails(
            collection.Id,
            collection.Name ?? string.Empty,
            collection.Overview,
            TmdbMovieMapper.NormalizeImagePath(collection.PosterPath),
            TmdbMovieMapper.NormalizeImagePath(collection.BackdropPath),
            collection.Parts
                .Select(ToCollectionProviderPart)
                .ToList());
    }

    private static CollectionProviderPart ToCollectionProviderPart(TmdbCollectionPartJson part) =>
        new(
            part.Id,
            part.Title ?? string.Empty,
            part.OriginalTitle,
            part.Overview,
            TmdbMovieMapper.NormalizeImagePath(part.PosterPath),
            TmdbMovieMapper.NormalizeImagePath(part.BackdropPath),
            TmdbMovieMapper.ParseReleaseDate(part.ReleaseDate),
            part.VoteAverage,
            part.VoteCount,
            part.Adult);
}
