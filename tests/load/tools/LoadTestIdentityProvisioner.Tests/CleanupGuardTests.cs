using MovieApp.LoadTestIdentityProvisioner;

namespace MovieApp.LoadTestIdentityProvisioner.Tests;

public sealed class CleanupGuardTests
{
    [Fact]
    public void CleanupRefusesNonLoad60HarnessId()
    {
        var manifest = new Load60IdentityManifest
        {
            Campaign = "load60",
            EmailDomain = Load60IdentityFormats.DefaultEmailDomain,
            Identities =
            [
                new Load60IdentityManifestEntry
                {
                    HarnessId = "real-user-001",
                    UserId = Guid.NewGuid(),
                    Email = "load60-001@loadtest.invalid",
                    NormalizedEmail = "load60-001@loadtest.invalid",
                    CreatedAtUtc = DateTime.UtcNow,
                },
            ],
        };

        Assert.False(Load60IdentityFormats.IsDedicatedLoad60HarnessId(manifest.Identities[0].HarnessId));
    }
}
