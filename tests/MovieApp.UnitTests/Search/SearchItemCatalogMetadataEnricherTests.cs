using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Catalog;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class SearchItemCatalogMetadataEnricherTests
{
    private static readonly Guid MovieId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TvId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task EnrichGenresAsync_LimitsAndOrdersGenresPerItem()
    {
        var repository = new FakeGenreReadRepository();
        var enricher = new SearchItemCatalogMetadataEnricher(repository);
        var canonical = new PaginatedResult<SearchItem>(
            [
                new SearchItem(MovieId, "movie", "Movie", null, null, null, null, null, 0m, 0, null),
                new SearchItem(TvId, "tv", "Show", null, null, null, null, null, 0m, 0, null),
            ],
            1,
            20,
            2,
            1);

        var result = await enricher.EnrichGenresAsync(canonical);

        Assert.Equal(["Drama", "Thriller"], result.Items[0].Genres);
        Assert.Equal(["Comedy"], result.Items[1].Genres);
        Assert.Equal(CatalogDisplayLimits.MaxListItemGenres, repository.LastMovieMaxGenres);
    }

    [Fact]
    public async Task EnrichGenresAsync_ReturnsEmptyForPersonLikeTypes()
    {
        var enricher = new SearchItemCatalogMetadataEnricher(new FakeGenreReadRepository());
        var canonical = new PaginatedResult<SearchItem>(
            [new SearchItem(Guid.NewGuid(), "person", "Actor", null, null, null, null, null, 0m, 0, null)],
            1,
            20,
            1,
            1);

        var result = await enricher.EnrichGenresAsync(canonical);

        Assert.Empty(result.Items[0].Genres ?? []);
    }

    private sealed class FakeGenreReadRepository : IGenreReadRepository
    {
        public int LastMovieMaxGenres { get; private set; }

        public Task<IReadOnlyList<(Guid Id, string Name)>> GetAllOrderedByNameAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<(Guid Id, string Name)>>([]);

        public Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
            IReadOnlyList<Guid> genreIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

        public Task<Guid?> GetIdByNameAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(null);

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetOrderedGenreNamesByMovieIdsAsync(
            IReadOnlyList<Guid> movieIds,
            int maxGenresPerItem,
            CancellationToken cancellationToken = default)
        {
            LastMovieMaxGenres = maxGenresPerItem;
            return Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>(
                new Dictionary<Guid, IReadOnlyList<string>>
                {
                    [MovieId] = ["Drama", "Thriller"],
                });
        }

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetOrderedGenreNamesByTvShowIdsAsync(
            IReadOnlyList<Guid> tvShowIds,
            int maxGenresPerItem,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>(
                new Dictionary<Guid, IReadOnlyList<string>>
                {
                    [TvId] = ["Comedy"],
                });
    }
}
