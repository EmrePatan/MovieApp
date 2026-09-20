using MovieApp.Api.Authentication;
using MovieApp.Api.Cors;
using MovieApp.Api.Diagnostics;
using MovieApp.Api.Errors;
using MovieApp.Api.ForwardedHeaders;
using MovieApp.Api.Identity;
using MovieApp.Api.RateLimiting;
using MovieApp.Api.Swagger;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace MovieApp.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddApiExceptionHandling();
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerWithBearerAuth();
        services.AddJwtAuthentication(configuration);
        services.AddConfiguredForwardedHeaders(configuration);
        services.AddTemporaryClientIpChainDiagnostic(configuration);
        services.AddConfiguredCors(configuration);
        services.Configure<SearchRateLimitOptions>(configuration.GetSection(SearchRateLimitOptions.SectionName));
        services.Configure<AccountRateLimitOptions>(configuration.GetSection(AccountRateLimitOptions.SectionName));
        services.Configure<TvShowFollowRateLimitOptions>(
            configuration.GetSection(TvShowFollowRateLimitOptions.SectionName));

        services.AddAuthRateLimiting(configuration);

        services.AddOptions<AppOptions>()
            .Bind(configuration.GetSection(AppOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AppOptions>, AppOptionsValidator>();

        return services;
    }
}
