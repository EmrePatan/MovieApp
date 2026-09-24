using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MovieApp.Api.BackgroundJobs;
using MovieApp.Api.EmailAssets;
using MovieApp.Api.Cors;
using MovieApp.Api.Errors;
using MovieApp.Api.ForwardedHeaders;
using MovieApp.Api.Health;
using MovieApp.Api.Observability;
using MovieApp.Api.Security;
using MovieApp.Application;
using MovieApp.Infrastructure;
using MovieApp.Infrastructure.Identity;
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
                .ReadFrom.Services(services)
                .Enrich.FromLogContext());

        builder.Services
            .AddApplication()
            .AddInfrastructure(builder.Configuration)
            .AddMovieAppDataProtection(builder.Configuration, builder.Environment)
            .AddApi(builder.Configuration)
            .AddBackgroundJobs(builder.Configuration)
            .AddEmailVerificationDelivery(builder.Configuration, builder.Environment)
            .AddPasswordResetDelivery(builder.Configuration, builder.Environment)
            .AddTvShowFollowBaselineProcessing(builder.Configuration, builder.Environment)
            .AddExternalRatingsRefreshProcessing(builder.Configuration, builder.Environment);
    }

    public static void ConfigurePipeline(WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<CorrelationIdMiddleware>();

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
            options.EnrichDiagnosticContext = static (diagnosticContext, httpContext) =>
            {
                var correlationId = CorrelationIdAccessor.Get(httpContext);
                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    diagnosticContext.Set("CorrelationId", correlationId);
                }

                var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? httpContext.User.FindFirstValue("sub");
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    diagnosticContext.Set("UserId", userId);
                }
            };

            options.GetLevel = static (httpContext, _, exception) =>
                RequestLoggingLevelPolicy.GetLevel(httpContext, exception);
        });
        app.UseProductionTransportSecurity();
        app.UseConfiguredCors();
        app.UseEmailAssetStaticFiles(app.Environment);
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks(
            "/health/ready",
            new HealthCheckOptions
            {
                ResponseWriter = HealthCheckResponseWriter.WriteResponse,
                ResultStatusCodes =
                {
                    [HealthStatus.Healthy] = StatusCodes.Status200OK,
                    [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
                    [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                }
            });
    }
}
