namespace MovieApp.Api.Security;

public static class SecurityPipelineExtensions
{
    public static bool ShouldApplyProductionTransportSecurity(IHostEnvironment environment) =>
        environment.IsProduction();

    public static WebApplication UseProductionTransportSecurity(this WebApplication app)
    {
        if (!ShouldApplyProductionTransportSecurity(app.Environment))
        {
            return app;
        }

        app.UseHsts();
        app.UseHttpsRedirection();
        return app;
    }
}
