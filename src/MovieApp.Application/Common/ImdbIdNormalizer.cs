namespace MovieApp.Application.Common;

public static class ImdbIdNormalizer
{
    public static string? Normalize(string? imdbId)
    {
        return string.IsNullOrWhiteSpace(imdbId) ? null : imdbId.Trim();
    }
}
