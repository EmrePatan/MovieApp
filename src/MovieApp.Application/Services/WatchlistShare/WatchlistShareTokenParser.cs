using System.Text.RegularExpressions;

namespace MovieApp.Application.Services.WatchlistShare;

public static partial class WatchlistShareTokenParser
{
    [GeneratedRegex(@"^[A-Za-z0-9_-]{32,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();

    public static bool IsValidPublicToken(string? token) =>
        !string.IsNullOrWhiteSpace(token) && TokenPattern().IsMatch(token.Trim());
}
