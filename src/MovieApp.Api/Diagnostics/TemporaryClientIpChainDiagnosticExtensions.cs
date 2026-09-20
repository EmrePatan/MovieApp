namespace MovieApp.Api.Diagnostics;

// TEMPORARY (#27 M4): remove after Render client-IP chain verification.
internal static class TemporaryClientIpChainDiagnosticExtensions
{
    internal static IServiceCollection AddTemporaryClientIpChainDiagnostic(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<TemporaryClientIpChainDiagnosticOptions>()
            .Bind(configuration.GetSection(TemporaryClientIpChainDiagnosticOptions.SectionName));

        return services;
    }

    internal static WebApplication UseTemporaryClientIpChainDiagnostic(this WebApplication app)
    {
        var enabled = app.Configuration
            .GetSection(TemporaryClientIpChainDiagnosticOptions.SectionName)
            .GetValue<bool>("Enabled");

        if (enabled)
        {
            app.UseMiddleware<TemporaryClientIpChainDiagnosticMiddleware>();
        }

        return app;
    }
}
