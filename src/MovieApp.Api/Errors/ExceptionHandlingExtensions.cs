using Microsoft.AspNetCore.Mvc;

namespace MovieApp.Api.Errors;

internal static class ExceptionHandlingExtensions
{
    internal static IServiceCollection AddApiExceptionHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                if (context.ProblemDetails is null)
                {
                    return;
                }

                var mapping = ApiExceptionMappings.FromStatusCode(context.ProblemDetails.Status);
                ApiProblemDetailsEnricher.Enrich(
                    context.HttpContext,
                    context.ProblemDetails,
                    mapping.Code);
            };
        });

        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<ApiProblemDetailsResultFilter>();
        });

        return services;
    }
}
