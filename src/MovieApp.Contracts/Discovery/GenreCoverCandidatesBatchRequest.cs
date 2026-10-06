namespace MovieApp.Contracts.Discovery;

public sealed record GenreCoverCandidatesBatchRequest(
    IReadOnlyList<string>? GenreIds,
    string? MediaType = null,
    int? CandidateCount = null);
