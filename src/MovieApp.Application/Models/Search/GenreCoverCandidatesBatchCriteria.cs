namespace MovieApp.Application.Models.Search;

public sealed record GenreCoverCandidatesBatchCriteria(
    IReadOnlyList<Guid> GenreIds,
    SearchContentType MediaType,
    int CandidateCount);
