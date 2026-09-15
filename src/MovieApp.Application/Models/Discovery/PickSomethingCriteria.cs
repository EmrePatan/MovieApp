using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Application.Models.Discovery;

public sealed record PickSomethingCriteria(
    RecommendationContentType MediaType,
    IReadOnlySet<Guid> SessionExcludedIds);
