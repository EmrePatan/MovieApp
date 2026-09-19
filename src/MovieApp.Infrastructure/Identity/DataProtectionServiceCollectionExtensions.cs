using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Infrastructure.Configuration;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Identity;

public static class DataProtectionServiceCollectionExtensions
{
    public static IServiceCollection AddMovieAppDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        services.AddOptions<MovieAppDataProtectionOptions>()
            .Bind(configuration.GetSection(MovieAppDataProtectionOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<MovieAppDataProtectionOptions>, MovieAppDataProtectionOptionsValidator>();
        services.AddSingleton<IEmailVerificationDeliverySecretProtector, DataProtectionEmailVerificationDeliverySecretProtector>();
        services.AddSingleton<IPasswordResetDeliverySecretProtector, DataProtectionPasswordResetDeliverySecretProtector>();

        var dataProtectionOptions = configuration
            .GetSection(MovieAppDataProtectionOptions.SectionName)
            .Get<MovieAppDataProtectionOptions>() ?? new MovieAppDataProtectionOptions();

        var postgreSqlOptions = configuration
            .GetSection(PostgreSqlOptions.SectionName)
            .Get<PostgreSqlOptions>() ?? new PostgreSqlOptions();

        var builder = services
            .AddDataProtection()
            .SetApplicationName(dataProtectionOptions.ApplicationName);

        if (hostEnvironment.IsEnvironment("Testing"))
        {
            return services;
        }

        if (postgreSqlOptions.IsConfigured())
        {
            builder.PersistKeysToDbContext<ApplicationDbContext>();
        }
        else if (hostEnvironment.IsDevelopment())
        {
            var keyRingDirectory = new DirectoryInfo(
                dataProtectionOptions.ResolveDevelopmentKeyRingPath(hostEnvironment));
            builder.PersistKeysToFileSystem(keyRingDirectory);
        }

        var encryptionKey = dataProtectionOptions.TryGetKeyEncryptionKey();
        if (encryptionKey is not null)
        {
            builder.Services.AddSingleton(new MovieAppDataProtectionKeyMaterial(encryptionKey));
            builder.Services.AddSingleton<IXmlEncryptor>(new DataProtectionAesGcmXmlEncryptor(encryptionKey));
            builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
                new ConfigureOptions<KeyManagementOptions>(options =>
                {
                    options.XmlEncryptor = sp.GetRequiredService<IXmlEncryptor>();
                }));
        }

        return services;
    }
}
