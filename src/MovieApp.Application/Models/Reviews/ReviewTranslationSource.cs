namespace MovieApp.Application.Models.Reviews;

public sealed record ReviewTranslationSource(
    Guid Id,
    string Content,
    DateTime UpdatedAt);
