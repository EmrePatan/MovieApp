using System.Text.RegularExpressions;

namespace MovieApp.Application.Services.Videos;

public static partial class YouTubeKeyValidator
{
    private static readonly Regex ValidKeyPattern = ValidKeyRegex();

    public static bool IsValid(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return ValidKeyPattern.IsMatch(key);
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$")]
    private static partial Regex ValidKeyRegex();
}
