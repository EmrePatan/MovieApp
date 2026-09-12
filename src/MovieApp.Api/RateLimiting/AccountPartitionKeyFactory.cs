namespace MovieApp.Api.RateLimiting;

internal static class AccountPartitionKeyFactory
{
    internal static string Create(HttpContext httpContext)
    {
        var userId = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}:{httpContext.Request.Path.Value ?? "unknown"}";
        }

        return $"ip:{ClientIpResolver.GetClientIpAddress(httpContext)}:{httpContext.Request.Path.Value ?? "unknown"}";
    }
}
