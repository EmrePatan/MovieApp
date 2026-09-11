using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Models.Home;

public sealed record HomeCriteria(
    SearchContentType Type,
    int SectionSize);
