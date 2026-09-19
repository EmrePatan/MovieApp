using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Infrastructure.Identity;

namespace MovieApp.UnitTests.Identity;

public sealed class DataProtectionKeyPersistenceTests
{
    [Fact]
    public void ProtectedDeliverySecretRemainsDecryptableAfterProviderRestart()
    {
        var keyRingDirectory = new DirectoryInfo(
            Path.Combine(Path.GetTempPath(), "movieapp-dp-tests", Guid.NewGuid().ToString("N")));

        var protectedPayload = ProtectWithPersistedKeys(keyRingDirectory);
        var rawToken = UnprotectWithPersistedKeys(keyRingDirectory, protectedPayload);

        Assert.Equal("integration-restart-token", rawToken);
    }

    private static string ProtectWithPersistedKeys(DirectoryInfo keyRingDirectory)
    {
        var services = new ServiceCollection();
        services
            .AddDataProtection()
            .SetApplicationName("MovieApp")
            .PersistKeysToFileSystem(keyRingDirectory);
        services.AddSingleton<IEmailVerificationDeliverySecretProtector, DataProtectionEmailVerificationDeliverySecretProtector>();

        using var provider = services.BuildServiceProvider();
        var protector = provider.GetRequiredService<IEmailVerificationDeliverySecretProtector>();
        return protector.Protect("integration-restart-token");
    }

    private static string UnprotectWithPersistedKeys(DirectoryInfo keyRingDirectory, string protectedPayload)
    {
        var services = new ServiceCollection();
        services
            .AddDataProtection()
            .SetApplicationName("MovieApp")
            .PersistKeysToFileSystem(keyRingDirectory);
        services.AddSingleton<IEmailVerificationDeliverySecretProtector, DataProtectionEmailVerificationDeliverySecretProtector>();

        using var provider = services.BuildServiceProvider();
        var protector = provider.GetRequiredService<IEmailVerificationDeliverySecretProtector>();
        return protector.Unprotect(protectedPayload);
    }
}
