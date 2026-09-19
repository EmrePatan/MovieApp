namespace MovieApp.Infrastructure.Providers.Tmdb;

internal static class TmdbCanonicalRequestPath
{
    public static string WithCanonicalLanguage(string relativePath, string canonicalLanguage) =>
        TmdbRequestPath.WithLanguage(relativePath, canonicalLanguage);
}
