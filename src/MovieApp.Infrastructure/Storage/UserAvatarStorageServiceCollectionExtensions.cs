using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Storage;
using MovieApp.Application.Configuration;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.Storage;

public static class UserAvatarStorageServiceCollectionExtensions
{
    public static IServiceCollection AddUserAvatarStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AvatarStorageOptions>()
            .Bind(configuration.GetSection(AvatarStorageOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AvatarStorageOptions>, AvatarStorageOptionsValidator>();

        services.AddSingleton<IUserAvatarBlobStorage>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AvatarStorageOptions>>().Value;
            if (options.IsConfigured)
            {
                return new S3CompatibleUserAvatarBlobStorage(sp.GetRequiredService<IOptions<AvatarStorageOptions>>());
            }

            // Development without S3: persist to disk so uploaded avatars/covers can actually be served.
            if (sp.GetRequiredService<IHostEnvironment>().IsDevelopment())
            {
                return new LocalDiskUserAvatarBlobStorage(LocalDiskUserAvatarBlobStorage.DefaultRootPath);
            }

            return new InMemoryUserAvatarBlobStorage();
        });

        return services;
    }
}
