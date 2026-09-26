using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MovieApp.Application;
using MovieApp.Infrastructure;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.ContentSearchTitleOps;

internal static class ContentSearchTitleOpsHost
{
    public static ServiceProvider CreateServiceProvider()
    {
        var apiDevelopmentSettings = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "MovieApp.Api", "appsettings.Development.json"));

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddJsonFile(apiDevelopmentSettings, optional: true)
            .AddUserSecrets(Assembly.GetExecutingAssembly())
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new OpsHostEnvironment());
        services.AddLogging(builder => builder.AddSimpleConsole());
        services.AddApplication();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    private sealed class OpsHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "ContentSearchTitleOps";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new PhysicalFileProvider(AppContext.BaseDirectory);
    }

    public static async Task PrintStatsAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var movies = await dbContext.Movies.CountAsync(cancellationToken);
        var tvShows = await dbContext.TvShows.CountAsync(cancellationToken);
        var cstTotal = await dbContext.ContentSearchTitles.CountAsync(cancellationToken);

        Console.WriteLine($"movies={movies}");
        Console.WriteLine($"tv_shows={tvShows}");
        Console.WriteLine($"content_search_titles={cstTotal}");

        var breakdown = await dbContext.ContentSearchTitles
            .GroupBy(row => new { row.ContentType, row.TitleKind, row.Source })
            .Select(group => new
            {
                group.Key.ContentType,
                group.Key.TitleKind,
                group.Key.Source,
                Count = group.Count(),
            })
            .OrderBy(row => row.ContentType)
            .ThenBy(row => row.TitleKind)
            .ThenBy(row => row.Source)
            .ToListAsync(cancellationToken);

        foreach (var row in breakdown)
        {
            Console.WriteLine(
                $"cst contentType={row.ContentType} titleKind={row.TitleKind} source={row.Source} count={row.Count}");
        }
    }
}
