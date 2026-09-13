namespace MovieApp.Application.Models.Recommendations;

public sealed record SimilaritySourceGenreRequest(Guid SourceId, IReadOnlyList<Guid> GenreIds);
