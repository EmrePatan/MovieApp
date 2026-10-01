using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Catalog;

namespace MovieApp.Application.Services.Search;

public sealed class SearchItemCatalogMetadataEnricher(IGenreReadRepository genreReadRepository)
{
    public async Task<PaginatedResult<SearchItem>> EnrichGenresAsync(
        PaginatedResult<SearchItem> result,
        CancellationToken cancellationToken = default)
    {
        if (result.Items.Count == 0)
        {
            return result;
        }

        var movieIds = result.Items
            .Where(item => string.Equals(item.Type, "movie", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .Distinct()
            .ToList();
        var tvShowIds = result.Items
            .Where(item => string.Equals(item.Type, "tv", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .Distinct()
            .ToList();

        var movieGenres = await genreReadRepository.GetOrderedGenreNamesByMovieIdsAsync(
            movieIds,
            CatalogDisplayLimits.MaxListItemGenres,
            cancellationToken);
        var tvGenres = await genreReadRepository.GetOrderedGenreNamesByTvShowIdsAsync(
            tvShowIds,
            CatalogDisplayLimits.MaxListItemGenres,
            cancellationToken);

        var enrichedItems = result.Items
            .Select(item => item with
            {
                Genres = ResolveGenres(item, movieGenres, tvGenres),
            })
            .ToList();

        return result with { Items = enrichedItems };
    }

    private static IReadOnlyList<string> ResolveGenres(
        SearchItem item,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> movieGenres,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> tvGenres)
    {
        if (string.Equals(item.Type, "movie", StringComparison.OrdinalIgnoreCase) &&
            movieGenres.TryGetValue(item.Id, out var movieGenreNames))
        {
            return movieGenreNames;
        }

        if (string.Equals(item.Type, "tv", StringComparison.OrdinalIgnoreCase) &&
            tvGenres.TryGetValue(item.Id, out var tvGenreNames))
        {
            return tvGenreNames;
        }

        return Array.Empty<string>();
    }
}
