using MovieApp.Contracts.Search;

namespace MovieApp.Contracts.Discovery;

public sealed record GenreCoverCandidatesItemResponse(
    string GenreId,
    string Status,
    IReadOnlyList<SearchItemResponse> Candidates);
