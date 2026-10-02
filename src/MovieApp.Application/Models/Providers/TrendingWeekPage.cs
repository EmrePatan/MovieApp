namespace MovieApp.Application.Models.Providers;

public sealed record TrendingWeekPage(
    IReadOnlyList<TrendingWeekProviderItem> Items,
    int Page,
    int TotalResults,
    int TotalPages);
