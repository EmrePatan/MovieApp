using Microsoft.Extensions.Hosting;

namespace MovieApp.Infrastructure.Configuration;

public sealed class MovieAppDataProtectionOptions
{
    public const string SectionName = "DataProtection";

    public string ApplicationName { get; set; } = "MovieApp";

    public string KeyEncryptionKeyBase64 { get; set; } = string.Empty;

    public string? DevelopmentKeyRingPath { get; set; }

    public string ResolveDevelopmentKeyRingPath(IHostEnvironment hostEnvironment)
    {
        if (!string.IsNullOrWhiteSpace(DevelopmentKeyRingPath))
        {
            return DevelopmentKeyRingPath;
        }

        return Path.Combine(hostEnvironment.ContentRootPath, "data-protection-keys");
    }

    public byte[]? TryGetKeyEncryptionKey()
    {
        if (string.IsNullOrWhiteSpace(KeyEncryptionKeyBase64))
        {
            return null;
        }

        var key = Convert.FromBase64String(KeyEncryptionKeyBase64);
        if (key.Length != 32)
        {
            throw new InvalidOperationException(
                "DataProtection:KeyEncryptionKeyBase64 must decode to exactly 32 bytes.");
        }

        return key;
    }
}
