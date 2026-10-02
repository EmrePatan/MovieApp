namespace MovieApp.Application.Models.Search;

/// <summary>
/// Discover / Keşfet rail order. General trending is not part of this catalog.
/// </summary>
public static class DiscoverRailCatalog
{
    public const string Platforms = "platforms";

    public const string Genres = "genres";

    public const string WorldCinema = "world-cinema";

    public const string HiddenGems = "hidden-gems";

    public const string Popular = "popular";

    public const string NewReleases = "new-releases";

    public const string TopRated = "top-rated";

    public static readonly IReadOnlyList<string> Order =
    [
        Platforms,
        Genres,
        WorldCinema,
        HiddenGems,
        Popular,
        NewReleases,
        TopRated
    ];
}
