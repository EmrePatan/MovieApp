using Microsoft.OpenApi;

namespace MovieApp.Api.Swagger;

internal static class SwaggerBearerExtensions
{
    internal static IServiceCollection AddSwaggerWithBearerAuth(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Enter your JWT access token."
            });

            options.OperationFilter<AuthorizeCheckOperationFilter>();
        });

        return services;
    }
}
