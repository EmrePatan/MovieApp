using System.Security.Cryptography;
using System.Text;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class HomeGlobalCacheKeys
{
    public const string Prefix = "home-global:";

    public const string Version = "v1";

    public static string Create(SearchContentType type, int sectionSize, string genreFingerprint) =>
        $"{Prefix}{type}:{sectionSize}:{genreFingerprint}:{Version}";

    public static string CreateGenreFingerprint(IReadOnlyList<string> genreSections)
    {
        if (genreSections.Count == 0)
        {
            return "none";
        }

        var joined = string.Join('\u001f', genreSections);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
    }
}
