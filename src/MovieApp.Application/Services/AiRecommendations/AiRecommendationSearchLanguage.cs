namespace MovieApp.Application.Services.AiRecommendations;

internal static class AiRecommendationSearchLanguage
{
    internal static string? ToTmdbLanguage(string? responseLanguage)
    {
        if (string.IsNullOrWhiteSpace(responseLanguage))
        {
            return null;
        }

        if (responseLanguage.StartsWith("tr", StringComparison.OrdinalIgnoreCase))
        {
            return "tr-TR";
        }

        if (responseLanguage.StartsWith("de", StringComparison.OrdinalIgnoreCase))
        {
            return "de-DE";
        }

        if (responseLanguage.StartsWith("fr", StringComparison.OrdinalIgnoreCase))
        {
            return "fr-FR";
        }

        if (responseLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase))
        {
            return "es-ES";
        }

        return null;
    }
}
