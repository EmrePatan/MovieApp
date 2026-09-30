namespace MovieApp.Application.Services.Keywords;

public static class KeywordLocalizationTranslationValidator
{
    public const int MaxNameLength = 200;

    public static bool TryValidateTranslatedName(
        string? translatedName,
        out string normalizedName,
        out string? failureReason)
    {
        normalizedName = string.Empty;
        failureReason = null;

        if (string.IsNullOrWhiteSpace(translatedName))
        {
            failureReason = "blank_translation";
            return false;
        }

        var trimmed = translatedName.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            failureReason = "translation_too_long";
            return false;
        }

        normalizedName = KeywordDiscoverLocalizationSupport.NormalizeSearchName(trimmed);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            failureReason = "blank_normalized_name";
            return false;
        }

        return true;
    }
}
