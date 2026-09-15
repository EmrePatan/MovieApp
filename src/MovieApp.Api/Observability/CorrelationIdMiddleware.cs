using System.Text.RegularExpressions;
using Serilog.Context;

namespace MovieApp.Api.Observability;

internal sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private static readonly Regex ValidCorrelationIdPattern = new(
        "^[A-Za-z0-9._:-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        CorrelationIdAccessor.Set(context, correlationId);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdConstants.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(CorrelationIdConstants.SerilogPropertyName, correlationId))
        {
            await next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var supplied = context.Request.Headers[CorrelationIdConstants.HeaderName].FirstOrDefault()
            ?? context.Request.Headers[CorrelationIdConstants.AlternateHeaderName].FirstOrDefault();

        if (IsValidCorrelationId(supplied))
        {
            return supplied!;
        }

        return GenerateCorrelationId();
    }

    private static bool IsValidCorrelationId(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= CorrelationIdConstants.MaxLength
        && ValidCorrelationIdPattern.IsMatch(value);

    private static string GenerateCorrelationId() =>
        Guid.NewGuid().ToString("N");
}