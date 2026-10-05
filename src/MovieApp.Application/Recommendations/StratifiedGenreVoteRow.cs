namespace MovieApp.Application.Recommendations;

public readonly record struct StratifiedGenreVoteRow(
    Guid GenreId,
    Guid ContentId,
    int VoteCount,
    decimal VoteAverage);
