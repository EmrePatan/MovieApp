namespace MovieApp.Contracts.Home;

public sealed record HomeSectionResponse(
    string Type,
    string Title,
    IReadOnlyList<HomeItemResponse> Items,
    int DisplayOrder);
