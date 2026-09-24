using System.Text.Json;
using System.Text.Json.Serialization;

namespace MovieApp.LoadTestIdentityProvisioner;

public sealed class Load60IdentityManifestEntry
{
    public required string HarnessId { get; init; }

    public required Guid UserId { get; init; }

    public required string Email { get; init; }

    public required string NormalizedEmail { get; init; }

    public required DateTime CreatedAtUtc { get; init; }
}

public sealed class Load60IdentityManifest
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required string Campaign { get; init; }

    public required string EmailDomain { get; init; }

    public required IReadOnlyList<Load60IdentityManifestEntry> Identities { get; init; }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void EnsureNoSecrets(Load60IdentityManifest manifest)
    {
        foreach (var entry in manifest.Identities)
        {
            if (entry.HarnessId.Contains("bearer", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Manifest must not contain token fields.");
            }
        }
    }

    public async Task WriteAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureNoSecrets(this);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(this, JsonOptions), cancellationToken);
    }

    public static async Task<Load60IdentityManifest> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var manifest = JsonSerializer.Deserialize<Load60IdentityManifest>(json, JsonOptions)
            ?? throw new InvalidOperationException("Manifest JSON is empty or invalid.");
        if (manifest.Identities.Count == 0)
        {
            throw new InvalidOperationException("Manifest contains no identities.");
        }

        return manifest;
    }
}
