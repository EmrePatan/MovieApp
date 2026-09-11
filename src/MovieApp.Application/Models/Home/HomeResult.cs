namespace MovieApp.Application.Models.Home;

public sealed record HomeResult(
    IReadOnlyList<HomeSection> Sections,
    bool IsPersonalized);
