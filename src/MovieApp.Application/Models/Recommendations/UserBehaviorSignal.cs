namespace MovieApp.Application.Models.Recommendations;

public sealed record UserBehaviorSignal(
    Guid ContentId,
    string ContentType,
    string SignalType,
    string? Title,
    int? RatingScore,
    DateTime? SignalAtUtc,
    IReadOnlyList<Guid> GenreIds,
    IReadOnlyDictionary<Guid, string> GenreNames,
    IReadOnlyList<Guid> PersonIds);
