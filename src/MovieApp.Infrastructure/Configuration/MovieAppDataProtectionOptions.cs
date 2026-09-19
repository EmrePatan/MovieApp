using Microsoft.Extensions.Hosting;

namespace MovieApp.Infrastructure.Configuration;

public sealed class MovieAppDataProtectionOptions
{
    public const string SectionName = "DataProtection";

    public string ApplicationName { get; set; } = "MovieApp";

    public string KeyRingRedisKey { get; set; } = "DataProtection-Keys";

    public string CertificatePath { get; set; } = string.Empty;

    public string CertificatePassword { get; set; } = string.Empty;

    public string? DevelopmentKeyRingPath { get; set; }

    public string ResolveRedisKey(string redisInstanceName)
    {
        var instancePrefix = string.IsNullOrWhiteSpace(redisInstanceName)
            ? "MovieApp:"
            : redisInstanceName;

        return $"{instancePrefix}{KeyRingRedisKey}";
    }

    public string ResolveDevelopmentKeyRingPath(IHostEnvironment hostEnvironment)
    {
        if (!string.IsNullOrWhiteSpace(DevelopmentKeyRingPath))
        {
            return DevelopmentKeyRingPath;
        }

        return Path.Combine(hostEnvironment.ContentRootPath, "data-protection-keys");
    }
}
