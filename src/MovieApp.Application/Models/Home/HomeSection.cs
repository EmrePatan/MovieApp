namespace MovieApp.Application.Models.Home;

public sealed record HomeSection(
    HomeSectionType Type,
    string Title,
    IReadOnlyList<HomeItem> Items,
    int DisplayOrder);
