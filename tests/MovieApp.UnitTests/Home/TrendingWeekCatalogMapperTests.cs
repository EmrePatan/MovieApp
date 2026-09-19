using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Home;

namespace MovieApp.UnitTests.Home;

public sealed class TrendingWeekCatalogMapperTests
{
    private static readonly Guid MovieId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    private static readonly Guid TvId = Guid.Parse("22222222-2222-2222-2222-222222222201");
    private static readonly Guid MovieTwoId = Guid.Parse("11111111-1111-1111-1111-111111111102");

    [Fact]
    public void MapOrderedItemsPreservesTmdbOrderAcrossMovieAndTv()
    {
        var providerItems = new[]
        {
            CreateProviderItem("tv", 920001, "Trending Show One"),
            CreateProviderItem("movie", 910001, "Trending Movie One"),
            CreateProviderItem("movie", 910002, "Trending Movie Two"),
        };

        var mapped = TrendingWeekCatalogMapper.MapOrderedItems(
            providerItems,
            new Dictionary<int, Guid>
            {
                [910001] = MovieId,
                [910002] = MovieTwoId,
            },
            new Dictionary<int, Guid>
            {
                [920001] = TvId,
            });

        Assert.Equal(
            ["Trending Show One", "Trending Movie One", "Trending Movie Two"],
            mapped.Select(item => item.Title).ToList());
        Assert.Equal(["tv", "movie", "movie"], mapped.Select(item => item.Type).ToList());
    }

    [Fact]
    public void MapOrderedItemsMapsMovieAndTvFields()
    {
        var providerItems = new[]
        {
            CreateProviderItem("movie", 910001, "Trending Movie One"),
            CreateProviderItem("tv", 920001, "Trending Show One"),
        };

        var mapped = TrendingWeekCatalogMapper.MapOrderedItems(
            providerItems,
            new Dictionary<int, Guid> { [910001] = MovieId },
            new Dictionary<int, Guid> { [920001] = TvId });

        Assert.Equal(MovieId, mapped[0].Id);
        Assert.Equal("movie", mapped[0].Type);
        Assert.Equal(910001, mapped[0].TmdbId);
        Assert.Equal(TvId, mapped[1].Id);
        Assert.Equal("tv", mapped[1].Type);
        Assert.Equal(920001, mapped[1].TmdbId);
    }

    [Fact]
    public void MapOrderedItemsSkipsUnmappedCatalogIds()
    {
        var providerItems = new[]
        {
            CreateProviderItem("movie", 910001, "Trending Movie One"),
            CreateProviderItem("tv", 920001, "Trending Show One"),
        };

        var mapped = TrendingWeekCatalogMapper.MapOrderedItems(
            providerItems,
            new Dictionary<int, Guid> { [910001] = MovieId },
            new Dictionary<int, Guid>());

        Assert.Single(mapped);
        Assert.Equal("Trending Movie One", mapped[0].Title);
    }

    private static TrendingWeekProviderItem CreateProviderItem(string mediaType, int tmdbId, string title) =>
        new(
            mediaType,
            tmdbId,
            title,
            null,
            "Overview",
            new DateOnly(2025, 1, 1),
            "/poster.jpg",
            "/backdrop.jpg",
            8.1m,
            1200);
}
