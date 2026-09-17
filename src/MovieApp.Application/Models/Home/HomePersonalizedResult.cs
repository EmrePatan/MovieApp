namespace MovieApp.Application.Models.Home;

public sealed record HomePersonalizedResult(
    IReadOnlyList<HomeSection> Sections,
    bool IsPersonalized,
    DateTime GeneratedAtUtc);
