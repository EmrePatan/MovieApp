using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Infrastructure.Configuration;
using StackExchange.Redis;

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

        var dataProtectionOptions = configuration
            .GetSection(MovieAppDataProtectionOptions.SectionName)
            .Get<MovieAppDataProtectionOptions>() ?? new MovieAppDataProtectionOptions();

        var redisOptions = configuration
            .GetSection(RedisOptions.SectionName)
            .Get<RedisOptions>() ?? new RedisOptions();

        var builder = services
            .AddDataProtection()
            .SetApplicationName(dataProtectionOptions.ApplicationName);

        if (hostEnvironment.IsEnvironment("Testing"))
        {
            return services;
        }

        if (redisOptions.IsConfigured())
        {
            var redisKey = new RedisKey(dataProtectionOptions.ResolveRedisKey(redisOptions.InstanceName));
            builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
                new ConfigureOptions<KeyManagementOptions>(options =>
                {
                    options.XmlRepository = new RedisXmlRepository(
                        () => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase(),
                        redisKey);
                }));
        }
        else if (hostEnvironment.IsDevelopment())
        {
            var keyRingDirectory = new DirectoryInfo(
                dataProtectionOptions.ResolveDevelopmentKeyRingPath(hostEnvironment));
            builder.PersistKeysToFileSystem(keyRingDirectory);
        }

        if (!string.IsNullOrWhiteSpace(dataProtectionOptions.CertificatePath))
        {
            builder.ProtectKeysWithCertificate(DataProtectionCertificateLoader.Load(dataProtectionOptions));
        }

        return services;
    }
}
