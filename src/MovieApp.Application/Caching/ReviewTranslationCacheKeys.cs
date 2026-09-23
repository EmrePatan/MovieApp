using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MovieApp.Application.Caching;

public static class ReviewTranslationCacheKeys
{
    private const string Version = "v1";

    public static string For(Guid reviewId, DateTime updatedAtUtc, string targetContentLocale)
    {
        var normalizedTarget = targetContentLocale.Trim().ToLowerInvariant();
        var versionSegment = updatedAtUtc.ToString("O", CultureInfo.InvariantCulture);
        var hash = ComputeShortHash($"{reviewId:N}:{versionSegment}");

        return $"review-translation:{reviewId:N}:{hash}:{normalizedTarget}:{Version}";
    }

    private static string ComputeShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes, 0, 8).ToLowerInvariant();
    }
}
