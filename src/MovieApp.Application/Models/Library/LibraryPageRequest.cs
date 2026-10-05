using MovieApp.Application.Common;
using MovieApp.Application.Library;

namespace MovieApp.Application.Models.Library;

public sealed record LibraryPageRequest(
    int Page,
    int PageSize,
    int FetchLimit,
    LibraryKeysetCursor? AfterCursor,
    LibraryCountMode CountMode,
    SearchTextMatch TitleMatch = default,
    string? TitleMatchContentLocale = null);
