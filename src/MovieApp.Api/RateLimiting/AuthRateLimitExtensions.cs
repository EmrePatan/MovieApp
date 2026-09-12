using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Api.RateLimiting;

internal static class AuthRateLimitExtensions
{
    internal static IServiceCollection AddAuthRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(AuthRateLimitOptions.SectionName)
            .Get<AuthRateLimitOptions>() ?? new AuthRateLimitOptions();

        services.Configure<AuthRateLimitOptions>(configuration.GetSection(AuthRateLimitOptions.SectionName));

        services.AddRateLimiter(rateLimiterOptions =>
        {
            rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            rateLimiterOptions.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new
                    {
                        status = StatusCodes.Status429TooManyRequests,
                        title = "Too many requests.",
                        detail = "Too many attempts. Please try again later."
                    },
                    cancellationToken);
            };

            rateLimiterOptions.AddPolicy(AuthRateLimitPolicies.Login, httpContext =>
                CreateFixedWindowPolicy(httpContext, options.LoginPermitLimit, options.LoginWindowMinutes));

            rateLimiterOptions.AddPolicy(AuthRateLimitPolicies.Register, httpContext =>
                CreateFixedWindowPolicy(httpContext, options.RegisterPermitLimit, options.RegisterWindowMinutes));

            rateLimiterOptions.AddPolicy(AuthRateLimitPolicies.ForgotPassword, httpContext =>
                CreateFixedWindowPolicy(
                    httpContext,
                    options.ForgotPasswordPermitLimit,
                    options.ForgotPasswordWindowMinutes));

            rateLimiterOptions.AddPolicy(AuthRateLimitPolicies.ResetPassword, httpContext =>
                CreateFixedWindowPolicy(
                    httpContext,
                    options.ResetPasswordPermitLimit,
                    options.ResetPasswordWindowMinutes));
        });

        return services;
    }

    private static RateLimitPartition<string> CreateFixedWindowPolicy(
        HttpContext httpContext,
        int permitLimit,
        int windowMinutes)
    {
        var clientIp = ClientIpResolver.GetClientIpAddress(httpContext);
        var endpoint = httpContext.Request.Path.Value ?? "unknown";
        var partitionKey = $"{clientIp}:{endpoint}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, permitLimit),
                Window = TimeSpan.FromMinutes(Math.Max(1, windowMinutes)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    }
}
