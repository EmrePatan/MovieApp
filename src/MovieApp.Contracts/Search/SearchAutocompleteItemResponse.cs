namespace MovieApp.Contracts.Search;

public sealed record SearchAutocompleteItemResponse(
    Guid Id,
    string Type,
    string Title,
    string? PosterUrl,
    int? TmdbId = null,
    string? KnownForDepartment = null);
