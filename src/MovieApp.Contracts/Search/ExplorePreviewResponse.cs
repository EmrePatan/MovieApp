namespace MovieApp.Contracts.Search;

public sealed record ExplorePreviewResponse(
    SearchResponse Trending,
    SearchResponse TopRated,
    SearchResponse NewReleases);
