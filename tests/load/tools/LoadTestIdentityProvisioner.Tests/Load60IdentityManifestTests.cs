using System.Text.Json;
using MovieApp.LoadTestIdentityProvisioner;

namespace MovieApp.LoadTestIdentityProvisioner.Tests;

public sealed class Load60IdentityManifestTests
{
    [Fact]
    public void ManifestSerializationOmitsSecretFieldNames()
    {
        var manifest = new Load60IdentityManifest
        {
            Campaign = "load60",
            EmailDomain = Load60IdentityFormats.DefaultEmailDomain,
            Identities =
            [
                new Load60IdentityManifestEntry
                {
                    HarnessId = "load60-001",
                    UserId = Guid.NewGuid(),
                    Email = "load60-001@loadtest.invalid",
                    NormalizedEmail = "load60-001@loadtest.invalid",
                    CreatedAtUtc = DateTime.UtcNow,
                },
            ],
        };

        var json = JsonSerializer.Serialize(manifest, Load60IdentityManifest.JsonOptions);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bearerToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecurityStamp", json, StringComparison.OrdinalIgnoreCase);
    }
}
