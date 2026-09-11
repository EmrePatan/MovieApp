namespace MovieApp.Application.Validation;

public static class SearchQueryValidator
{
    public const int MinimumLength = 2;
    public const int MaximumLength = 100;

    public static SearchQueryValidationResult Validate(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return SearchQueryValidationResult.Failure("Search query is required.");
        }

        var trimmedQuery = query.Trim();

        if (trimmedQuery.Length < MinimumLength)
        {
            return SearchQueryValidationResult.Failure(
                $"Search query must be at least {MinimumLength} characters.");
        }

        if (trimmedQuery.Length > MaximumLength)
        {
            return SearchQueryValidationResult.Failure(
                $"Search query must not exceed {MaximumLength} characters.");
        }

        return SearchQueryValidationResult.Success();
    }
}
