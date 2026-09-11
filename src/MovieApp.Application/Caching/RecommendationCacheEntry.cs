using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Application.Caching;

public sealed class RecommendationCacheEntry
{
    public PaginatedResult<RecommendationItem> Result { get; init; } = null!;
}
