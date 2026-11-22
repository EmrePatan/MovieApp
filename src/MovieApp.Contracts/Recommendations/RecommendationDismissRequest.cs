namespace MovieApp.Contracts.Recommendations;

/// <summary>
/// "Not interested" action for a recommended title. ContentType is "movie" or "tv".
/// </summary>
public sealed record RecommendationDismissRequest(string ContentType, Guid ContentId);
