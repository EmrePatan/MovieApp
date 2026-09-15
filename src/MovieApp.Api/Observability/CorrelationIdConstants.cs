namespace MovieApp.Api.Observability;

internal static class CorrelationIdConstants
{
    internal const string HeaderName = "X-Correlation-Id";
    internal const string AlternateHeaderName = "X-Request-Id";
    internal const int MaxLength = 128;
    internal const string HttpContextItemKey = "CorrelationId";
    internal const string SerilogPropertyName = "CorrelationId";
}