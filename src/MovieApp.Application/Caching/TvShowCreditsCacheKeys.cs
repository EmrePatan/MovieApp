namespace MovieApp.Application.Caching;

public static class TvShowCreditsCacheKeys
{
    public const string Prefix = "tvshow-credits:";

    public const string Version = "v1";

    public static string Create(Guid tvShowId) => $"{Prefix}{tvShowId}:{Version}";
}
