using System.Security.Cryptography;
using System.Text;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Keywords;

public static class KeywordLocalizationSourceText
{
    public static string ResolveSourceText(Keyword keyword)
    {
        var canonical = keyword.CanonicalName?.Trim();
        if (!string.IsNullOrEmpty(canonical))
        {
            return canonical;
        }

        return keyword.Name.Trim();
    }

    public static string ComputeSourceTextHash(string normalizedSourceText)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedSourceText));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string NormalizeSourceTextForHash(string sourceText) =>
        sourceText.Trim();
}
