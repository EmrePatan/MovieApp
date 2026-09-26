using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Search;

public static class ContentSearchTitleKindPrecedence
{
    public static bool IsHigherPriority(ContentSearchTitleKind candidate, ContentSearchTitleKind current) =>
        (int)candidate < (int)current;
}
