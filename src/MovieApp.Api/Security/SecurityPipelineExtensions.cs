namespace MovieApp.Api.Security;

public static class SecurityPipelineExtensions
{
    public static bool ShouldApplyProductionTransportSecurity(IHostEnvironment environment) =>
        environment.IsProduction();

    public static bool ShouldEnableInProcessHttpsRedirection()
    {
        var httpsPorts = Environment.GetEnvironmentVariable("ASPNETCORE_HTTPS_PORTS");
        if (!string.IsNullOrWhiteSpace(httpsPorts))
        {
            return true;
        }

        var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        return !string.IsNullOrWhiteSpace(urls) &&
               urls.Contains("https://", StringComparison.OrdinalIgnoreCase);
    }

    public static WebApplication UseProductionTransportSecurity(this WebApplication app)
    {
        if (!ShouldApplyProductionTransportSecurity(app.Environment))
        {
            return app;
        }

        if (!ShouldEnableInProcessHttpsRedirection())
        {
            return app;
        }

        app.UseHsts();
        app.UseHttpsRedirection();
        return app;
    }
}
