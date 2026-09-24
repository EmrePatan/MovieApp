namespace MovieApp.LoadTestIdentityProvisioner;

internal static class ProvisionerCli
{
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var map = ParseArgs(args.Skip(1));

        if (command == "token-requirements")
        {
            PrintTokenRequirements(map);
            return 0;
        }

        var connection = map.GetValueOrDefault("connection")
            ?? Environment.GetEnvironmentVariable("LOAD_TEST_PG_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException(
                "PostgreSQL connection is required via --connection or LOAD_TEST_PG_CONNECTION (never commit this value).");
        }

        var manifestPath = map.GetValueOrDefault("manifest")
            ?? Environment.GetEnvironmentVariable("LOAD_TEST_LOAD60_MANIFEST")
            ?? Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "data", "load60-identities.manifest.json"));

        return command switch
        {
            "provision" => await RunProvisionAsync(map, connection, manifestPath),
            "cleanup" => await RunCleanupAsync(map, connection, manifestPath),
            _ => throw new InvalidOperationException($"Unknown command '{command}'."),
        };
    }

    private static async Task<int> RunProvisionAsync(Dictionary<string, string> map, string connection, string manifestPath)
    {
        var count = int.Parse(map.GetValueOrDefault("count") ?? "50", System.Globalization.CultureInfo.InvariantCulture);
        var emailDomain = map.GetValueOrDefault("email-domain") ?? Load60IdentityFormats.DefaultEmailDomain;
        var confirm = map.ContainsKey("confirm-production");
        var dryRun = !confirm;

        if (!Load60IdentityFormats.IsReservedDomainSupported(emailDomain))
        {
            throw new InvalidOperationException(
                $"Domain '{emailDomain}' fails validation. Pass --email-domain with an operator-owned domain.");
        }

        Console.WriteLine($"Mode: {(dryRun ? "DRY RUN" : "WRITE")}");
        Console.WriteLine($"Target identities: {count}");
        Console.WriteLine($"Email domain: {emailDomain}");
        Console.WriteLine($"Manifest path: {manifestPath}");

        var result = await ProvisionerService.RunAsync(new ProvisionOptions
        {
            ConnectionString = connection,
            Count = count,
            EmailDomain = emailDomain,
            DryRun = dryRun,
            ConfirmProduction = confirm,
            ManifestPath = manifestPath,
        });

        foreach (var plan in result.Analysis.Slots)
        {
            Console.WriteLine($"  [{plan.Status}] {plan.Slot.HarnessId} {plan.Slot.Email} {plan.Detail}".Trim());
        }

        Console.WriteLine(result.Message);
        if (result.IsBlocked)
        {
            return 2;
        }

        if (!result.IsDryRun && result.Manifest is not null)
        {
            Console.WriteLine($"Created {result.CreatedCount} new user(s). Manifest written.");
        }

        return 0;
    }

    private static async Task<int> RunCleanupAsync(Dictionary<string, string> map, string connection, string manifestPath)
    {
        var confirmDelete = map.ContainsKey("confirm-delete");
        var dryRun = !confirmDelete;

        Console.WriteLine($"Cleanup mode: {(dryRun ? "DRY RUN" : "DELETE")}");
        Console.WriteLine($"Manifest: {manifestPath}");

        var result = await CleanupService.RunAsync(new CleanupOptions
        {
            ConnectionString = connection,
            ManifestPath = manifestPath,
            DryRun = dryRun,
            ConfirmDelete = confirmDelete,
        });

        if (result.Refused)
        {
            Console.Error.WriteLine(result.Message);
            return 2;
        }

        if (result.Targets is not null)
        {
            foreach (var target in result.Targets)
            {
                Console.WriteLine($"  [{target.Status}] {target.Entry.HarnessId} {target.Entry.UserId} {target.Detail}".Trim());
            }
        }

        Console.WriteLine(result.Message);
        return 0;
    }

    private static void PrintTokenRequirements(Dictionary<string, string> map)
    {
        var count = int.Parse(map.GetValueOrDefault("count") ?? "50", System.Globalization.CultureInfo.InvariantCulture);
        var min = LoadTestStageTokenRequirements.RecommendedMinMinutesUntilStageStart(count);
        Console.WriteLine(LoadTestStageTokenRequirements.ExplainRecommendation(count));
        Console.WriteLine($"Recommended -MinMinutesUntilExpiry: {min}");
    }

    private static Dictionary<string, string> ParseArgs(IEnumerable<string> args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? key = null;
        foreach (var arg in args)
        {
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                var body = arg[2..];
                if (body.Contains('=', StringComparison.Ordinal))
                {
                    var parts = body.Split('=', 2);
                    map[parts[0]] = parts[1];
                    key = null;
                }
                else if (body is "confirm-production" or "confirm-delete")
                {
                    map[body] = "true";
                    key = null;
                }
                else
                {
                    key = body;
                }
            }
            else if (key is not null)
            {
                map[key] = arg;
                key = null;
            }
        }

        return map;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            LoadTestIdentityProvisioner — operator-local LOAD60 identity tooling (#60)

            Commands:
              provision   Plan or create dedicated LOAD60 users (default: dry run)
              cleanup     Plan or delete manifest users (default: dry run)
              token-requirements  Print JWT preflight window guidance

            Common options:
              --connection <cs>     PostgreSQL (or env LOAD_TEST_PG_CONNECTION)
              --count 50
              --email-domain loadtest.invalid
              --manifest <path>

            provision write:
              --confirm-production  (prompts for campaign password; not stored)

            cleanup delete:
              --confirm-delete
            """);
    }
}
