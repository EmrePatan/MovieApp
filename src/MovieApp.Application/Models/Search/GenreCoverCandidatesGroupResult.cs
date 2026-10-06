namespace MovieApp.Application.Models.Search;

public sealed record GenreCoverCandidatesGroupResult(
    Guid GenreId,
    DiscoveryBatchItemStatus Status,
    IReadOnlyList<SearchItem> Candidates);
