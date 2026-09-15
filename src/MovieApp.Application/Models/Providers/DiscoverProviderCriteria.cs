using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Models.Providers;

public sealed record DiscoverProviderCriteria(
    DiscoverBrowseMode Mode,
    int Page,
    IReadOnlyList<int> GenreTmdbIds,
    int? Year,
    decimal? MinRating,
    string? Language,
    DiscoverBrowseSort? Sort);
