namespace MovieApp.Contracts.Home;

public sealed record HomeResponse(
    IReadOnlyList<HomeSectionResponse> Sections,
    bool IsPersonalized);
