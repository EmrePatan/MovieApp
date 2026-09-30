using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Localization;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.KeywordLocalizationOps;

internal static class KeywordLocalizationOpsCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        await using var provider = KeywordLocalizationOpsHost.CreateServiceProvider();
        return args[0].ToLowerInvariant() switch
        {
            "backfill" => await RunBackfillAsync(provider, args.Skip(1).ToArray()),
            "coverage" => await RunCoverageAsync(provider),
            _ => UnknownCommand(args[0]),
        };
    }

    private static async Task<int> RunBackfillAsync(ServiceProvider provider, string[] args)
    {
        var options = provider.GetRequiredService<IOptions<KeywordLocalizationBackfillOptions>>().Value;
        var batchSize = options.DefaultBatchSize;
        var maxItems = options.DefaultMaxItems;
        var maxConcurrency = 1;
        var delayMs = 250;
        var dryRun = false;
        var allLocales = false;
        string? locale = null;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--locale" when index + 1 < args.Length:
                    locale = args[++index];
                    break;
                case "--batch-size" when index + 1 < args.Length:
                    batchSize = int.Parse(args[++index], CultureInfo.InvariantCulture);
                    break;
                case "--max-items" when index + 1 < args.Length:
                    maxItems = int.Parse(args[++index], CultureInfo.InvariantCulture);
                    break;
                case "--max-concurrency" when index + 1 < args.Length:
                    maxConcurrency = int.Parse(args[++index], CultureInfo.InvariantCulture);
                    break;
                case "--delay-ms" when index + 1 < args.Length:
                    delayMs = int.Parse(args[++index], CultureInfo.InvariantCulture);
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--all-locales":
                    allLocales = true;
                    break;
            }
        }

        batchSize = Math.Clamp(batchSize, 1, options.MaxBatchSize);
        maxItems = Math.Max(1, maxItems);

        if (maxConcurrency > 1)
        {
            Console.Error.WriteLine(
                "Parallel provider batches are not enabled yet; use --max-concurrency 1.");
            return 1;
        }

        if (maxConcurrency < 1)
        {
            Console.Error.WriteLine("--max-concurrency must be at least 1.");
            return 1;
        }

        if (!allLocales && string.IsNullOrWhiteSpace(locale))
        {
            Console.Error.WriteLine("Specify --locale <locale> or --all-locales.");
            return 1;
        }

        await using var scope = provider.CreateAsyncScope();
        var backfill = scope.ServiceProvider.GetRequiredService<IKeywordLocalizationBackfillService>();
        var locales = allLocales
            ? SupportedContentLocales.KeywordBulkTranslationTargetLocales
            : [SupportedContentLocales.Normalize(locale!)];

        var exitCode = 0;
        foreach (var targetLocale in locales)
        {
            var request = new KeywordLocalizationBackfillRequest(
                targetLocale,
                batchSize,
                maxItems,
                delayMs,
                maxConcurrency,
                dryRun);

            if (dryRun)
            {
                var plan = await backfill.PlanDryRunAsync(request);
                Console.WriteLine(
                    $"dry_run locale={plan.Locale} eligible={plan.EligibleCandidates} missing={plan.Missing} stale_machine={plan.StaleMachine} protected={plan.ProtectedCuratedOrReviewed} would_translate={plan.WouldTranslate}");
                continue;
            }

            var result = await backfill.BackfillAsync(request);
            Console.WriteLine(
                $"locale={result.Locale} selected={result.Selected} translated={result.Translated} inserted={result.Inserted} updated={result.Updated} skipped_current={result.SkippedCurrent} skipped_curated={result.SkippedCurated} stale_review_required={result.StaleReviewRequired} failed={result.Failed} provider_requests={result.ProviderRequests} elapsed_ms={result.ElapsedMilliseconds}");
        }

        return exitCode;
    }

    private static async Task<int> RunCoverageAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var coverage = scope.ServiceProvider.GetRequiredService<IKeywordLocalizationCoverageService>();
        var rows = await coverage.GetCoverageAsync();
        foreach (var row in rows)
        {
            Console.WriteLine(
                $"{row.Locale} eligible={row.Eligible} localized={row.Localized} coverage={row.CoveragePercent}% missing={row.Missing} machine_unreviewed={row.MachineUnreviewed} reviewed={row.Reviewed} curated={row.Curated} stale={row.Stale}");
        }

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
        Console.WriteLine("KeywordLocalizationOps commands:");
        Console.WriteLine("  coverage");
        Console.WriteLine("  backfill --locale tr-TR [--batch-size N] [--max-items N] [--delay-ms N] [--max-concurrency 1] [--dry-run]");
        Console.WriteLine("  backfill --all-locales [--max-items N] [--dry-run] ...");
    }
}
