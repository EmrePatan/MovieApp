namespace MovieApp.Application.Services.Videos;

public static class YouTubeWatchUrlBuilder
{
    public const string CanonicalHost = "www.youtube.com";

    public static string BuildWatchUrl(string key)
    {
        if (!YouTubeKeyValidator.IsValid(key))
        {
            throw new ArgumentException("Invalid YouTube video key.", nameof(key));
        }

        return $"https://{CanonicalHost}/watch?v={key}";
    }
}
