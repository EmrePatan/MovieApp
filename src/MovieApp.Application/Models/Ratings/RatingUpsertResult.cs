namespace MovieApp.Application.Models.Ratings;

public sealed record RatingUpsertResult(RatingResult Rating, bool Created);
