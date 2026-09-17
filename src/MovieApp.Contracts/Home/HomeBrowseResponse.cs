namespace MovieApp.Contracts.Home;

public sealed record HomeBrowseResponse(
    IReadOnlyList<HomeSectionResponse> Sections,
    DateTime GeneratedAtUtc);
