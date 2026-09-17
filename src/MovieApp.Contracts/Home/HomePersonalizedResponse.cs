namespace MovieApp.Contracts.Home;

public sealed record HomePersonalizedResponse(
    IReadOnlyList<HomeSectionResponse> Sections,
    bool IsPersonalized,
    DateTime GeneratedAtUtc);
