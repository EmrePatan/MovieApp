namespace MovieApp.Contracts.Search;

public sealed record ExplorePreviewResponse(
    IReadOnlyList<string> RailOrder,
    SearchResponse HiddenGems,
    SearchResponse Popular,
    SearchResponse NewReleases,
    SearchResponse TopRated);
