namespace MovieApp.Contracts.ExternalRatings;

public sealed record ExternalRatingsResponse(
    DateTime? FetchedAtUtc,
    bool IsStale,
    IReadOnlyList<ExternalRatingResponse> Ratings);
