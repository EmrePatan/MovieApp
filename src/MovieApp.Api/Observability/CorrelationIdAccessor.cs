namespace MovieApp.Api.Observability;

internal static class CorrelationIdAccessor
{
    internal static string? Get(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(CorrelationIdConstants.HttpContextItemKey, out var value)
            ? value as string
            : null;

    internal static void Set(HttpContext httpContext, string correlationId) =>
        httpContext.Items[CorrelationIdConstants.HttpContextItemKey] = correlationId;
}
