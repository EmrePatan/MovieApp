namespace MovieApp.Contracts.Discovery;

public sealed record GenreCoverCandidatesBatchResponse(
    IReadOnlyList<GenreCoverCandidatesItemResponse> Items);
