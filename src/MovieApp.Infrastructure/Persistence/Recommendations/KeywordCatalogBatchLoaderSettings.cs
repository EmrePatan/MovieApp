namespace MovieApp.Infrastructure.Persistence.Recommendations;

/// <summary>
/// Raw Npgsql batch loaders bypass EF; use an explicit command timeout for full-catalog aggregates.
/// </summary>
internal static class KeywordCatalogBatchLoaderSettings
{
    internal const int CommandTimeoutSeconds = 180;
}
