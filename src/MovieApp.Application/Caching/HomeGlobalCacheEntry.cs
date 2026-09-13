using MovieApp.Application.Models.Home;

namespace MovieApp.Application.Caching;

public sealed class HomeGlobalCacheEntry
{
    public HomeSection Trending { get; init; } =
        new(HomeSectionType.Trending, "Trending", [], 0);

    public HomeSection Popular { get; init; } =
        new(HomeSectionType.Popular, "Popular", [], 0);

    public HomeSection NewReleases { get; init; } =
        new(HomeSectionType.NewReleases, "New Releases", [], 0);

    public HomeSection TopRated { get; init; } =
        new(HomeSectionType.TopRated, "Top Rated", [], 0);

    public List<HomeSection> GenreSections { get; init; } = [];
}
