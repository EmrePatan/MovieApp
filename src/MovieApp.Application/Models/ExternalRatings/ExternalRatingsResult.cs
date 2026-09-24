namespace MovieApp.Application.Models.ExternalRatings;

public sealed record ExternalRatingsResult(
    DateTime? FetchedAtUtc,
    bool IsStale,
    IReadOnlyList<ExternalRatingItem> Ratings);
