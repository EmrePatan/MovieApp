namespace MovieApp.Application.Models.Home;

public sealed record HomeBrowseResult(
    IReadOnlyList<HomeSection> Sections,
    DateTime GeneratedAtUtc);
