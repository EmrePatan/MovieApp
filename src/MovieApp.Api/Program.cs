using MovieApp.Api;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

ApplicationBootstrap.ConfigureBuilder(builder);

var app = builder.Build();

ApplicationBootstrap.ConfigurePipeline(app);

try
{
    Log.Information("Starting MovieApp API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MovieApp API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
