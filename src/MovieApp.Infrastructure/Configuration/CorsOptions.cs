namespace MovieApp.Infrastructure.Configuration;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>
    /// When false, CORS middleware is not registered. Native mobile clients do not require browser CORS.
    /// </summary>
    public bool Enabled { get; set; }

    public string[] AllowedOrigins { get; set; } = [];

    public IReadOnlyList<string> GetValidOrigins(bool requireHttps) =>
        AllowedOrigins
            .Where(origin => CorsOriginRules.IsValidOrigin(origin, requireHttps))
            .Select(origin => origin.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
