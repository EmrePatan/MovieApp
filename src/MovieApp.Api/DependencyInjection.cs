using MovieApp.Api.Authentication;
using MovieApp.Api.ForwardedHeaders;
using MovieApp.Api.Identity;
using MovieApp.Api.RateLimiting;
using MovieApp.Api.Swagger;
using MovieApp.Application.Abstractions.Identity;

namespace MovieApp.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerWithBearerAuth();
        services.AddJwtAuthentication(configuration);
        services.AddConfiguredForwardedHeaders(configuration);
        services.AddAuthRateLimiting(configuration);

        return services;
    }
}
