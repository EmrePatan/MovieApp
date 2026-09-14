using MovieApp.Api.BackgroundJobs;
using MovieApp.Api.Cors;
using MovieApp.Api.Errors;
using MovieApp.Api.ForwardedHeaders;
using MovieApp.Api.Security;
using MovieApp.Application;
using MovieApp.Infrastructure;
using Serilog;
using Serilog.Events;

namespace MovieApp.Api;

public static class ApplicationBootstrap
{
    public static void ConfigureBuilder(WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services));

        builder.Services
            .AddApplication()
            .AddInfrastructure(builder.Configuration)
            .AddApi(builder.Configuration)
            .AddBackgroundJobs(builder.Configuration);
    }

    public static void ConfigurePipeline(WebApplication app)
    {
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "MovieApp API v1");
            });
        }

        app.UseConfiguredForwardedHeaders();
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = static (httpContext, _, exception) =>
            {
                if (exception is not null
                    && RequestAbortExceptionHandling.IsRequestAbortedCancellation(httpContext, exception))
                {
                    return LogEventLevel.Debug;
                }

                if (exception is not null)
                {
                    return LogEventLevel.Error;
                }

                return httpContext.Response.StatusCode > 499
                    ? LogEventLevel.Error
                    : LogEventLevel.Information;
            };
        });
        app.UseProductionTransportSecurity();
        app.UseConfiguredCors();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/health/ready");
    }
}
