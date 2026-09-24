namespace MovieApp.Contracts.ExternalRatings;

public sealed record ExternalRatingResponse(
    string Source,
    decimal Value,
    int Scale,
    long? Votes);
