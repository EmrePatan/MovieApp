namespace MovieApp.Application.Models.Search;

public sealed record SearchSuggestion(Guid Id, string Type, string Title, string? PosterUrl);
