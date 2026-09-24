using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Models.Reviews;

public sealed record ReviewListPageResult(
    PaginatedResult<ReviewResult> Page,
    IReadOnlyDictionary<int, int> ReviewScoreDistribution);
