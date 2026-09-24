using MovieApp.LoadTestIdentityProvisioner;

namespace MovieApp.LoadTestIdentityProvisioner.Tests;

public sealed class ProvisionOptionsTests
{
    [Fact]
    public void WriteRequiresConfirmProduction()
    {
        var options = new ProvisionOptions
        {
            ConnectionString = "Host=localhost",
            ManifestPath = "manifest.json",
            DryRun = false,
            ConfirmProduction = false,
        };

        Assert.Throws<InvalidOperationException>(() => options.EnsureWriteAuthorized());
    }

    [Fact]
    public void DryRunDoesNotRequireConfirmProduction()
    {
        var options = new ProvisionOptions
        {
            ConnectionString = "Host=localhost",
            ManifestPath = "manifest.json",
            DryRun = true,
            ConfirmProduction = false,
        };

        options.EnsureWriteAuthorized();
    }
}
