using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Api.Cors;

public static class CorsPolicyNames
{
    public const string Default = "MovieApp.Default";
}

public static class CorsExtensions
{
    public static IServiceCollection AddConfiguredCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<CorsOptions>, CorsOptionsValidator>();

        var corsOptions = configuration
            .GetSection(CorsOptions.SectionName)
            .Get<CorsOptions>() ?? new CorsOptions();

        if (!corsOptions.Enabled)
        {
            return services;
        }

        var requireHttps = !IsDevelopmentOrTesting(configuration);
        var allowedOrigins = corsOptions.GetValidOrigins(requireHttps);

        if (allowedOrigins.Count == 0)
        {
            return services;
        }

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyNames.Default, policy =>
            {
                policy
                    .WithOrigins(allowedOrigins.ToArray())
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }

    public static WebApplication UseConfiguredCors(this WebApplication app)
    {
        var corsOptions = app.Configuration
            .GetSection(CorsOptions.SectionName)
            .Get<CorsOptions>() ?? new CorsOptions();

        if (!corsOptions.Enabled)
        {
            return app;
        }

        var requireHttps = !app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing");
        if (corsOptions.GetValidOrigins(requireHttps).Count == 0)
        {
            return app;
        }

        app.UseCors(CorsPolicyNames.Default);
        return app;
    }

    private static bool IsDevelopmentOrTesting(IConfiguration configuration)
    {
        var environment = configuration["ASPNETCORE_ENVIRONMENT"];
        return string.Equals(environment, Environments.Development, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(environment, "Testing", StringComparison.OrdinalIgnoreCase);
    }
}
