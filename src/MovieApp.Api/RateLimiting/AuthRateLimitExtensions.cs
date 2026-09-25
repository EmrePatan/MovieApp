using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using MovieApp.Api.Errors;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Api.RateLimiting;

internal static class AuthRateLimitExtensions
{
    private static readonly Func<HttpContext, string> ClientIpEndpointPartitionKeyFactory =
        DistributedRateLimitPolicyFactory.CreateClientIpEndpointPartitionKeyFactory();

    internal static IServiceCollection AddAuthRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AuthRateLimitOptions>(configuration.GetSection(AuthRateLimitOptions.SectionName));

        services.AddRateLimiter(rateLimiterOptions =>
        {
            ConfigureRejectionResponse(rateLimiterOptions);

            rateLimiterOptions.AddPolicy(AuthRateLimitPolicies.Login, httpContext =>
            {
                var options = httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
                return CreateAuthDistributedPolicy(
                    httpContext,
                    AuthRateLimitPolicies.Login,
                    options.LoginPermitLimit,
                    options.LoginWindowMinutes);
            });

            rateLimiterOptions.AddPolicy(AuthRateLimitPolicies.Social, httpContext =>
            {
                var options = httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
                return CreateAuthDistributedPolicy(
                    httpContext,
                    AuthRateLimitPolicies.Social,
                    options.SocialPermitLimit,
                    options.SocialWindowMinutes);
            });

            rateLimiterOptions.AddPolicy(AuthRateLimitPolicies.Register, httpContext =>
            {
                var options = httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
                return CreateAuthDistributedPolicy(
                    httpContext,
                    AuthRateLimitPolicies.Register,
                    options.RegisterPermitLimit,
                    options.RegisterWindowMinutes);
            });

            rateLimiterOptions.AddPolicy(AuthRateLimitPolicies.ForgotPassword, httpContext =>
            {
                var options = httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
                return CreateAuthDistributedPolicy(
                    httpContext,
                    AuthRateLimitPolicies.ForgotPassword,
                    options.ForgotPasswordPermitLimit,
                    options.ForgotPasswordWindowMinutes);
            });

            rateLimiterOptions.AddPolicy(AuthRateLimitPolicies.ResetPassword, httpContext =>
            {
                var options = httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
                return CreateAuthDistributedPolicy(
                    httpContext,
                    AuthRateLimitPolicies.ResetPassword,
                    options.ResetPasswordPermitLimit,
                    options.ResetPasswordWindowMinutes);
            });

            rateLimiterOptions.AddPolicy(AuthRateLimitPolicies.VerifyEmail, httpContext =>
            {
                var options = httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
                return CreateAuthDistributedPolicy(
                    httpContext,
                    AuthRateLimitPolicies.VerifyEmail,
                    options.VerifyEmailPermitLimit,
                    options.VerifyEmailWindowMinutes);
            });

            rateLimiterOptions.AddPolicy(AuthRateLimitPolicies.ResendVerification, httpContext =>
            {
                var options = httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
                return CreateAuthDistributedPolicy(
                    httpContext,
                    AuthRateLimitPolicies.ResendVerification,
                    options.ResendVerificationPermitLimit,
                    options.ResendVerificationWindowMinutes);
            });

            rateLimiterOptions.AddPolicy(AuthRateLimitPolicies.Refresh, httpContext =>
            {
                var options = httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
                return CreateAuthDistributedPolicy(
                    httpContext,
                    AuthRateLimitPolicies.Refresh,
                    options.RefreshPermitLimit,
                    options.RefreshWindowMinutes);
            });

            rateLimiterOptions.AddPolicy(
                ProductMetricsRateLimitPolicies.Increment,
                httpContext => CreateInMemoryFixedWindowPolicy(httpContext, 120, 1));

            ProductionRateLimitPolicyRegistration.AddPolicies(rateLimiterOptions, configuration);
        });

        return services;
    }

    private static RateLimitPartition<string> CreateAuthDistributedPolicy(
        HttpContext httpContext,
        string policyName,
        int permitLimit,
        int windowMinutes) =>
        DistributedRateLimitPolicyFactory.CreatePolicy(
            httpContext,
            policyName,
            permitLimit,
            windowMinutes,
            ClientIpEndpointPartitionKeyFactory);

    private static RateLimitPartition<string> CreateInMemoryFixedWindowPolicy(
        HttpContext httpContext,
        int permitLimit,
        int windowMinutes)
    {
        var partitionKey = ClientIpEndpointPartitionKeyFactory(httpContext);

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

    internal static void ConfigureRejectionResponse(RateLimiterOptions rateLimiterOptions)
    {
        rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        rateLimiterOptions.OnRejected = async (context, cancellationToken) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)Math.Ceiling(((TimeSpan)retryAfter).TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }

            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too Many Requests",
                Detail = "Too many attempts. Please try again later."
            };

            ApiProblemDetailsEnricher.Enrich(
                context.HttpContext,
                problemDetails,
                ApiErrorCodes.TooManyRequests);

            await context.HttpContext.Response.WriteAsJsonAsync(
                problemDetails,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);
        };
    }
}
