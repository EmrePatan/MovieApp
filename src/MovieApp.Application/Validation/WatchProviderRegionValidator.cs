namespace MovieApp.Application.Validation;

public static class WatchProviderRegionValidator
{
    public const string DefaultRegion = "TR";

    public static SearchQueryValidationResult Validate(string? region)
    {
        var normalized = Normalize(region);

        if (normalized.Length != 2 || !normalized.All(char.IsLetter))
        {
            return SearchQueryValidationResult.Failure("Region must be a two-letter ISO country code.");
        }

        return SearchQueryValidationResult.Success();
    }

    public static string Normalize(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            return DefaultRegion;
        }

        return region.Trim().ToUpperInvariant();
    }
}
