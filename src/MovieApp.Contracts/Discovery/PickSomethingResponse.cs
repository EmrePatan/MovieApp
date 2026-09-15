using MovieApp.Contracts.Recommendations;

namespace MovieApp.Contracts.Discovery;

public sealed record PickSomethingResponse(RecommendationItemResponse? Item);
