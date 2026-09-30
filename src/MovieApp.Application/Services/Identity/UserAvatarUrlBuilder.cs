namespace MovieApp.Application.Services.Identity;

public static class UserAvatarUrlBuilder
{
    public static string? BuildPublicUrl(string? publicBaseUrl, string? storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            return null;
        }

        var trimmedBase = publicBaseUrl.Trim().TrimEnd('/');
        var trimmedKey = storageKey.Trim().TrimStart('/');
        return $"{trimmedBase}/{trimmedKey}";
    }
}
