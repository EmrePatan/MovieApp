using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Home;

public sealed record HomeWeeklyTrendingCompositionResult(
    IReadOnlyList<SearchItem> HeroItems,
    IReadOnlyList<SearchItem> TrendingItems,
    HomeWeeklyTrendingReadSource ReadSource);
