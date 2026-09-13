namespace MovieApp.Application.Models.Home;

public sealed record HomeGlobalSections(
    HomeSection Trending,
    HomeSection Popular,
    HomeSection NewReleases,
    HomeSection TopRated,
    IReadOnlyList<HomeSection> GenreSections);
