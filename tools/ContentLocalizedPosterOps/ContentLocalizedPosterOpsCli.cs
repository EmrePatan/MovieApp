using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Services.Localization;

namespace MovieApp.ContentLocalizedPosterOps;

internal static class ContentLocalizedPosterOpsCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        await using var provider = ContentLocalizedPosterOpsHost.CreateServiceProvider();
        return args[0].ToLowerInvariant() switch
        {
            "backfill" => await RunBackfillAsync(provider, args.Skip(1).ToArray()),
            _ => UnknownCommand(args[0]),
        };
    }

    private static async Task<int> RunBackfillAsync(ServiceProvider provider, string[] args)
    {
        var batchSize = 25;
        var maxItems = 250;
        var maxConcurrency = 3;
        var delayMs = 250;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--batch-size" when index + 1 < args.Length:
                    batchSize = int.Parse(args[++index]);
                    break;
                case "--max-items" when index + 1 < args.Length:
                    maxItems = int.Parse(args[++index]);
                    break;
                case "--max-concurrency" when index + 1 < args.Length:
                    maxConcurrency = int.Parse(args[++index]);
                    break;
                case "--delay-ms" when index + 1 < args.Length:
                    delayMs = int.Parse(args[++index]);
                    break;
            }
        }

        await using var scope = provider.CreateAsyncScope();
        var backfill = scope.ServiceProvider.GetRequiredService<IContentLocalizedPosterBackfillService>();
        var result = await backfill.RunAsync(new ContentLocalizedPosterBackfillRequest(
            batchSize,
            maxItems,
            maxConcurrency,
            delayMs));

        Console.WriteLine(
            $"backfill processed={result.Processed} succeeded={result.Succeeded} failed={result.Failed} provider_calls={result.ProviderDetailCalls}");
        Console.WriteLine(
            $"resume last_movie_id={result.LastProcessedMovieId} last_tv_id={result.LastProcessedTvShowId}");
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("ContentLocalizedPosterOps commands:");
        Console.WriteLine("  backfill [--batch-size N] [--max-items N] [--max-concurrency N] [--delay-ms N]");
    }
}
