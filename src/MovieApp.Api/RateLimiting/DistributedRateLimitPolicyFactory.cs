using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using MovieApp.Application.Abstractions.RateLimiting;

namespace MovieApp.Api.RateLimiting;

internal static class DistributedRateLimitPolicyFactory
{
    internal static RateLimitPartition<string> CreatePolicy(
        HttpContext httpContext,
        string policyName,
        int permitLimit,
        int windowMinutes,
        Func<HttpContext, string> partitionKeyFactory)
    {
        var store = httpContext.RequestServices.GetRequiredService<IRateLimitCounterStore>();
        var partitionKey = partitionKeyFactory(httpContext);

        return RateLimitPartition.Get(
            partitionKey,
            _ => new DistributedFixedWindowRateLimiter(
                store,
                $"{policyName}:{partitionKey}",
                permitLimit,
                TimeSpan.FromMinutes(Math.Max(1, windowMinutes))));
    }

    internal static Func<HttpContext, string> CreateClientIpEndpointPartitionKeyFactory() =>
        context =>
        {
            var clientIp = ClientIpResolver.GetClientIpAddress(context);
            var endpoint = context.Request.Path.Value ?? "unknown";
            return $"{clientIp}:{endpoint}";
        };
}
