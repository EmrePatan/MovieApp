using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Home;

internal readonly record struct HomeWeeklyTrendingContentKey(string Type, Guid Id)
{
    internal static HomeWeeklyTrendingContentKey FromSearchItem(SearchItem item) =>
        new(item.Type, item.Id);
}
