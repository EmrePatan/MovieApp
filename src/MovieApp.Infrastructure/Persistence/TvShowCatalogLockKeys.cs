namespace MovieApp.Infrastructure.Persistence;

internal static class TvShowCatalogLockKeys
{
    private const int CatalogHydrationNamespace = 0x5F4B_C471;

    public static long ForCatalogHydration(Guid tvShowId) =>
        BitConverter.ToInt64(HashTvShowId(tvShowId), 0);
    
    private static byte[] HashTvShowId(Guid tvShowId)
    {
        var hash = new byte[8];
        var combined = HashCode.Combine(CatalogHydrationNamespace, tvShowId);
        BitConverter.TryWriteBytes(hash, combined);
        return hash;
    }
}
