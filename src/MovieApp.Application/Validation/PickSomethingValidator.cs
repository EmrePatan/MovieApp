using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Recommendations;

namespace MovieApp.Application.Validation;

public static class PickSomethingValidator
{
    public const int MaxSessionExcludedIds = 50;

    public static SearchQueryValidationResult Validate(PickSomethingCriteria criteria)
    {
        if (criteria.SessionExcludedIds.Count > MaxSessionExcludedIds)
        {
            return SearchQueryValidationResult.Failure(
                $"At most {MaxSessionExcludedIds} session exclusion IDs are allowed.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static SearchQueryValidationResult ValidateMediaType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return SearchQueryValidationResult.Success();
        }

        return RecommendationValidator.TryParseType(mediaType, out _)
            ? SearchQueryValidationResult.Success()
            : SearchQueryValidationResult.Failure("Media type must be one of: movie, tv, all.");
    }

    public static IReadOnlySet<Guid> ParseSessionExcludedIds(string[]? excludeIds)
    {
        if (excludeIds is null || excludeIds.Length == 0)
        {
            return new HashSet<Guid>();
        }

        var parsed = new HashSet<Guid>();

        foreach (var rawValue in excludeIds)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                continue;
            }

            foreach (var segment in rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Guid.TryParse(segment, out var id))
                {
                    parsed.Add(id);
                }
            }
        }

        return parsed;
    }
}
