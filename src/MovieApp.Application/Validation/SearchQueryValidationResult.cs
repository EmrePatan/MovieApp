namespace MovieApp.Application.Validation;

public sealed record SearchQueryValidationResult(bool IsValid, string? ErrorMessage)
{
    public static SearchQueryValidationResult Success() => new(true, null);

    public static SearchQueryValidationResult Failure(string errorMessage) => new(false, errorMessage);
}
