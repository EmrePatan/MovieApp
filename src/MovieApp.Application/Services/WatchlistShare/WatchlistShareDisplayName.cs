using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.WatchlistShare;

internal static class WatchlistShareDisplayName
{
    internal static string? ResolvePublicDisplayName(User user)
    {
        var displayName = user.DisplayName?.Trim();
        if (string.IsNullOrEmpty(displayName))
        {
            return null;
        }

        if (displayName.Contains('@', StringComparison.Ordinal))
        {
            return null;
        }

        if (string.Equals(displayName, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return displayName;
    }
}
