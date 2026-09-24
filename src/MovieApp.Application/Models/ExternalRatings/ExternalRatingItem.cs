namespace MovieApp.Application.Models.ExternalRatings;

public sealed record ExternalRatingItem(
    string Source,
    decimal Value,
    int Scale,
    long? Votes = null);
