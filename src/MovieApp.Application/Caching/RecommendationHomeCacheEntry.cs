using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Application.Caching;

public sealed class RecommendationHomeCacheEntry
{
    public IReadOnlyList<RecommendationSection> Sections { get; init; } = [];
}
