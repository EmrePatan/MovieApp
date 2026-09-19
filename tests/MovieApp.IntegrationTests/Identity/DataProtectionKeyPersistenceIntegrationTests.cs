using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Infrastructure.Identity;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.IntegrationTests.Identity;

public sealed class DataProtectionKeyPersistenceIntegrationTests
{
    [Fact]
    public async Task ProtectedDeliverySecretRemainsDecryptableAfterProviderRestartAgainstPostgreSql()
    {
        var connectionString = IntegrationTestDatabase.GetConnectionString("movieapp_data_protection_tests");
        var encryptionKey = Convert.ToBase64String(new byte[32]);

        await using (var context = CreateContext(connectionString))
        {
            await context.Database.EnsureCreatedAsync();
            await context.Database.ExecuteSqlRawAsync("""TRUNCATE TABLE "DataProtectionKeys";""");
        }

        var protectedPayload = await ProtectWithPersistedKeysAsync(connectionString, encryptionKey);
        var rawToken = await UnprotectWithPersistedKeysAsync(connectionString, encryptionKey, protectedPayload);

        Assert.Equal("integration-restart-token", rawToken);
    }

    private static ApplicationDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<string> ProtectWithPersistedKeysAsync(
        string connectionString,
        string encryptionKeyBase64)
    {
        await using var provider = BuildProvider(connectionString, encryptionKeyBase64);
        var protector = provider.GetRequiredService<IEmailVerificationDeliverySecretProtector>();
        return protector.Protect("integration-restart-token");
    }

    private static async Task<string> UnprotectWithPersistedKeysAsync(
        string connectionString,
        string encryptionKeyBase64,
        string protectedPayload)
    {
        await using var provider = BuildProvider(connectionString, encryptionKeyBase64);
        var protector = provider.GetRequiredService<IEmailVerificationDeliverySecretProtector>();
        return protector.Unprotect(protectedPayload);
    }

    private static ServiceProvider BuildProvider(string connectionString, string encryptionKeyBase64)
    {
        var encryptionKey = Convert.FromBase64String(encryptionKeyBase64);

        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services
            .AddDataProtection()
            .SetApplicationName("MovieApp")
            .PersistKeysToDbContext<ApplicationDbContext>();
        services.AddSingleton(new MovieAppDataProtectionKeyMaterial(encryptionKey));
        services.AddSingleton<IXmlEncryptor>(new DataProtectionAesGcmXmlEncryptor(encryptionKey));
        services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
            new ConfigureOptions<KeyManagementOptions>(options =>
            {
                options.XmlEncryptor = sp.GetRequiredService<IXmlEncryptor>();
            }));
        services.AddSingleton<IEmailVerificationDeliverySecretProtector, DataProtectionEmailVerificationDeliverySecretProtector>();

        return services.BuildServiceProvider();
    }
}
