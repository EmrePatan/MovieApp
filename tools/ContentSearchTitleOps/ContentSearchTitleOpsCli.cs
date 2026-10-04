using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Keywords;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Movies;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Models.Keywords;
using MovieApp.Domain.Enums;
using System.Data.Common;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.ContentSearchTitleOps;

internal static class ContentSearchTitleOpsCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        await using var provider = ContentSearchTitleOpsHost.CreateServiceProvider();
        var command = args[0].ToLowerInvariant();
        var options = ParseOptions(args.Skip(1).ToArray());

        return command switch
        {
            "stats" => await RunStatsAsync(provider),
            "canonical-backfill" => await RunCanonicalBackfillAsync(provider),
            "enrich" => await RunEnrichAsync(provider, options),
            "find-movie" => await RunFindMovieAsync(provider, options),
            "inspect-movie" => await RunInspectMovieAsync(provider, options),
            "search-probe" => await RunSearchProbeAsync(provider, options),
            "explain-cst" => await RunExplainCstAsync(provider, options),
            "ensure-movie" => await RunEnsureMovieAsync(provider, options),
            "keyword-graph-reconcile" => await RunKeywordGraphReconcileAsync(provider),
            "keyword-graph-verify-readiness" => await RunKeywordGraphVerifyReadinessAsync(provider),
            "mdb-keywords-movie" => await RunMdbKeywordsMovieAsync(provider, options),
            "mdb-keywords-tv" => await RunMdbKeywordsTvAsync(provider, options),
            "mdb-keywords-sync-movie" => await RunMdbKeywordsSyncMovieAsync(provider, options),
            "mdb-keywords-sync-tv" => await RunMdbKeywordsSyncTvAsync(provider, options),
            _ => UnknownCommand(command),
        };
    }

    private static async Task<int> RunMdbKeywordsMovieAsync(ServiceProvider provider, ParsedOptions options)
    {
        if (options.TmdbId is not int tmdbId)
        {
            Console.Error.WriteLine("mdb-keywords-movie requires --tmdb-id <id>");
            return 1;
        }

        return await RunMdbKeywordsAsync(provider, CatalogContentType.Movie, tmdbId);
    }

    private static async Task<int> RunMdbKeywordsTvAsync(ServiceProvider provider, ParsedOptions options)
    {
        if (options.TmdbId is not int tmdbId)
        {
            Console.Error.WriteLine("mdb-keywords-tv requires --tmdb-id <id>");
            return 1;
        }

        return await RunMdbKeywordsAsync(provider, CatalogContentType.Tv, tmdbId);
    }

    private static async Task<int> RunMdbKeywordsAsync(
        ServiceProvider provider,
        CatalogContentType mediaType,
        int tmdbId)
    {
        await using var scope = provider.CreateAsyncScope();
        var transport = scope.ServiceProvider.GetRequiredService<IMdbListKeywordTransportProvider>();
        var result = await transport.FetchKeywordsAsync(mediaType, tmdbId);
        if (result is null)
        {
            Console.WriteLine("mdb_keywords status=unavailable (MDBList not configured or transport failed)");
            return 2;
        }

        Console.WriteLine($"mdb_keywords media_type={result.MediaType} tmdb_id={result.TmdbId} not_found={result.IsNotFound} count={result.Keywords.Count}");
        foreach (var keyword in result.Keywords)
        {
            Console.WriteLine($"keyword external_id={keyword.ExternalId} name={keyword.Name}");
        }

        return 0;
    }

    private static async Task<int> RunMdbKeywordsSyncMovieAsync(ServiceProvider provider, ParsedOptions options)
    {
        if (options.TmdbId is not int tmdbId)
        {
            Console.Error.WriteLine("mdb-keywords-sync-movie requires --tmdb-id <id>");
            return 1;
        }

        return await RunMdbKeywordsSyncAsync(provider, tmdbId, isMovie: true);
    }

    private static async Task<int> RunMdbKeywordsSyncTvAsync(ServiceProvider provider, ParsedOptions options)
    {
        if (options.TmdbId is not int tmdbId)
        {
            Console.Error.WriteLine("mdb-keywords-sync-tv requires --tmdb-id <id>");
            return 1;
        }

        return await RunMdbKeywordsSyncAsync(provider, tmdbId, isMovie: false);
    }

    private static async Task<int> RunMdbKeywordsSyncAsync(ServiceProvider provider, int tmdbId, bool isMovie)
    {
        await using var scope = provider.CreateAsyncScope();
        var ingestion = scope.ServiceProvider.GetRequiredService<IMdbListKeywordIngestionService>();
        var result = isMovie
            ? await ingestion.IngestMovieByTmdbIdAsync(tmdbId)
            : await ingestion.IngestTvShowByTmdbIdAsync(tmdbId);

        if (result.Status != MdbListKeywordIngestionStatus.Succeeded || result.Stats is null)
        {
            Console.WriteLine($"mdb_keywords_sync status={result.Status}");
            return result.Status == MdbListKeywordIngestionStatus.CatalogNotFound ? 1 : 2;
        }

        var stats = result.Stats;
        Console.WriteLine(
            $"mdb_keywords_sync status={result.Status} provider_keywords={stats.ProviderKeywordCount} promoted={stats.PromotedCanonicalCount} skipped_no_match={stats.SkippedNoMatchCount} skipped_ambiguous={stats.SkippedAmbiguousCount} external_refs_created={stats.ExternalReferencesCreated} external_refs_reused={stats.ExternalReferencesReused} mdb_sources={result.MdbListSourceCount} synced_at_utc={result.MdbListKeywordsSyncedAtUtc:O}");
        return 0;
    }

    private static async Task<int> RunKeywordGraphReconcileAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IKeywordGraphReconciliationService>();
        var result = await service.ReconcileAsync();
        PrintKeywordGraphResult("reconcile", result);
        return result.IsReadyForProviderAwareSync ? 0 : 2;
    }

    private static async Task<int> RunKeywordGraphVerifyReadinessAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IKeywordGraphReconciliationService>();
        var result = await service.VerifyReadinessAsync();
        PrintKeywordGraphResult("verify-readiness", result);
        return result.IsReadyForProviderAwareSync ? 0 : 2;
    }

    private static void PrintKeywordGraphResult(string operation, MovieApp.Application.Models.Keywords.KeywordGraphReconciliationResult result)
    {
        Console.WriteLine($"keyword_graph_{operation} ready={result.IsReadyForProviderAwareSync}");
        Console.WriteLine($"keywords={result.KeywordCount} tmdb_refs={result.TmdbExternalReferenceCount}");
        Console.WriteLine($"missing_canonical={result.MissingCanonicalNameCount} missing_normalized={result.MissingNormalizedNameCount} missing_tmdb_refs={result.MissingTmdbExternalRefCount}");
        Console.WriteLine($"movie_joins={result.MovieKeywordJoinCount} missing_movie_sources={result.MissingMovieKeywordSourceCount} missing_movie_materialized_joins={result.MissingMovieMaterializedJoinCount}");
        Console.WriteLine($"tv_joins={result.TvShowKeywordJoinCount} missing_tv_sources={result.MissingTvShowKeywordSourceCount} missing_tv_materialized_joins={result.MissingTvShowMaterializedJoinCount}");
        Console.WriteLine($"conflicts={result.ConflictingExternalReferenceCount}");
        foreach (var conflict in result.Conflicts)
        {
            Console.WriteLine($"conflict: {conflict}");
        }
    }

    private static async Task<int> RunStatsAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await ContentSearchTitleOpsHost.PrintStatsAsync(dbContext, CancellationToken.None);
        return 0;
    }

    private static async Task<int> RunCanonicalBackfillAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var backfill = scope.ServiceProvider.GetRequiredService<IContentSearchTitleCatalogBackfillService>();
        var result = await backfill.BackfillCanonicalAndOriginalAsync();
        Console.WriteLine($"canonical_backfill movies={result.MoviesProcessed} tv={result.TvShowsProcessed}");
        return 0;
    }

    private static async Task<int> RunEnrichAsync(ServiceProvider provider, ParsedOptions options)
    {
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IContentSearchTitleProviderEnrichmentRepository>();
        var enrichment = scope.ServiceProvider.GetRequiredService<IContentSearchTitleProviderEnrichmentService>();

        var movieCount = await repository.CountMoviesWithTmdbIdAsync();
        var tvCount = await repository.CountTvShowsWithResolvableProviderIdAsync();
        Console.WriteLine($"eligible_movies={movieCount} eligible_tv={tvCount}");
        if (options.NeedsSearchTitleMetadataRepair)
        {
            var movieRepair = await repository.CountMoviesNeedingSearchTitleMetadataRepairAsync();
            var tvRepair = await repository.CountTvShowsNeedingSearchTitleMetadataRepairAsync();
            Console.WriteLine($"needs_repair_movies={movieRepair} needs_repair_tv={tvRepair}");
        }

        var request = new ContentSearchTitleProviderEnrichmentRequest
        {
            ContentType = options.ContentType,
            OnlyMovieId = options.OnlyMovieId,
            OnlyTvShowId = options.OnlyTvShowId,
            StartAfterMovieId = options.StartAfterMovieId,
            StartAfterTvShowId = options.StartAfterTvShowId,
            BatchSize = options.BatchSize ?? ContentSearchTitleProviderEnrichmentRequest.DefaultBatchSize,
            MaxItems = options.MaxItems ?? ContentSearchTitleProviderEnrichmentRequest.DefaultMaxItems,
            DelayBetweenRequestsMs = options.DelayMs ?? ContentSearchTitleProviderEnrichmentRequest.DefaultDelayBetweenRequestsMs,
            NeedsSearchTitleMetadataRepairOnly = options.NeedsSearchTitleMetadataRepair,
        };

        var result = await enrichment.EnrichFromProviderAsync(request);
        Console.WriteLine(
            $"enrich processed={result.Processed} succeeded={result.Succeeded} failed={result.Failed} skipped={result.Skipped} provider_calls={result.ProviderDetailCalls}");
        Console.WriteLine(
            $"resume last_movie_id={result.LastProcessedMovieId} last_tv_id={result.LastProcessedTvShowId}");
        return 0;
    }

    private static async Task<int> RunInspectMovieAsync(ServiceProvider provider, ParsedOptions options)
    {
        if (options.OnlyMovieId is null)
        {
            Console.Error.WriteLine("--only-movie-id is required for inspect-movie");
            return 1;
        }

        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var movie = await dbContext.Movies.AsNoTracking()
            .Where(row => row.Id == options.OnlyMovieId.Value)
            .Select(row => new { row.Id, row.Title, row.TmdbId })
            .FirstOrDefaultAsync();

        if (movie is null)
        {
            Console.WriteLine("movie_not_found");
            return 1;
        }

        Console.WriteLine($"movie id={movie.Id} title={movie.Title} tmdb_id={movie.TmdbId}");
        var rows = await dbContext.ContentSearchTitles.AsNoTracking()
            .Where(row => row.ContentId == movie.Id)
            .OrderBy(row => row.TitleKind)
            .ThenBy(row => row.Title)
            .ToListAsync();

        foreach (var row in rows)
        {
            Console.WriteLine(
                $"cst kind={row.TitleKind} source={row.Source} title={row.Title} normalized={row.NormalizedTitle}");
        }

        return 0;
    }

    private static async Task<int> RunSearchProbeAsync(ServiceProvider provider, ParsedOptions options)
    {
        if (options.OnlyMovieId is null)
        {
            Console.Error.WriteLine("--only-movie-id is required for search-probe");
            return 1;
        }

        var targetMovieId = options.OnlyMovieId.Value;
        var queries = options.Queries.Count > 0
            ? options.Queries
            :
            [
                "dönersen ısl",
                "dönersen isl",
                "donersen isl",
                "islik",
                "ıslık",
                "whistle if you",
                "WHISTLE IF YOU",
            ];

        await using var scope = provider.CreateAsyncScope();
        var searchRepository = scope.ServiceProvider.GetRequiredService<ISearchRepository>();
        var providerIngestion = scope.ServiceProvider.GetRequiredService<IUnifiedSearchProviderIngestionService>();
        var autocomplete = scope.ServiceProvider.GetRequiredService<IAutocompleteService>();
        const string contentLocale = "tr-TR";
        const int limit = 10;

        foreach (var query in queries)
        {
            var local = await searchRepository.AutocompleteAsync(query, limit);
            var providerSuggestions = await providerIngestion.GetAutocompleteSuggestionsAsync(
                query,
                limit,
                contentLocale);
            var merged = await autocomplete.GetSuggestionsAsync(query, contentLocale);

            var localHit = local.Any(item => item.Id == targetMovieId);
            var providerHit = providerSuggestions.Any(item => item.Id == targetMovieId);
            var mergedHit = merged.Any(item => item.Id == targetMovieId);

            var searchCriteria = new SearchCriteria(
                query,
                SearchContentType.Movie,
                null,
                null,
                null,
                null,
                SearchSortOption.Relevance,
                1,
                10);
            var searchPage = await searchRepository.SearchAsync(searchCriteria);
            var searchHit = searchPage.Items.Any(item => item.Id == targetMovieId);

            Console.WriteLine(
                $"query={query} local_ac={local.Count} provider_ac={providerSuggestions.Count} merged_ac={merged.Count} " +
                $"local_hit={localHit} provider_hit={providerHit} merged_hit={mergedHit} search_hit={searchHit} search_total={searchPage.TotalCount}");
        }

        return 0;
    }

    private static async Task<int> RunExplainCstAsync(ServiceProvider provider, ParsedOptions options)
    {
        var pattern = options.Title ?? "%islik%";
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            "EXPLAIN (ANALYZE, BUFFERS) SELECT \"Id\" FROM content_search_titles WHERE \"NormalizedTitle\" ILIKE @pattern LIMIT 20";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "pattern";
        parameter.Value = pattern;
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            Console.WriteLine(reader.GetString(0));
        }

        return 0;
    }

    private static async Task<int> RunEnsureMovieAsync(ServiceProvider provider, ParsedOptions options)
    {
        if (options.TmdbId is null)
        {
            Console.Error.WriteLine("--tmdb-id is required for ensure-movie");
            return 1;
        }

        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IGetMovieByTmdbIdService>();
        var result = await service.GetAsync(options.TmdbId.Value);
        Console.WriteLine($"ensure_movie id={result.Id} title={result.Title} tmdb_id={result.TmdbId}");
        return 0;
    }

    private static async Task<int> RunFindMovieAsync(ServiceProvider provider, ParsedOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Title) && options.TmdbId is null)
        {
            Console.Error.WriteLine("--title or --tmdb-id is required for find-movie");
            return 1;
        }

        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IContentSearchTitleProviderEnrichmentRepository>();
        if (options.TmdbId is not null)
        {
            var byTmdb = await repository.FindMovieIdByTmdbIdAsync(options.TmdbId.Value);
            Console.WriteLine(byTmdb?.ToString() ?? "not_found");
            return 0;
        }

        var id = await repository.FindMovieIdByTitleAsync(options.Title!);
        Console.WriteLine(id?.ToString() ?? "not_found");
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
        Console.WriteLine("""
            ContentSearchTitleOps — explicit content_search_titles backfill (not run on startup).

            Commands:
              stats
              canonical-backfill
              enrich [--max-items N] [--batch-size N] [--delay-ms N]
                     [--content-type movie|tv]
                     [--needs-search-title-metadata-repair]
                     [--only-movie-id GUID] [--only-tv-id GUID]
                     [--after-movie-id GUID] [--after-tv-id GUID]
              find-movie --title "Whistle If You Come Back"
              find-movie --tmdb-id 378435
              inspect-movie --only-movie-id GUID
              search-probe --only-movie-id GUID [--query "text" ...]
              explain-cst [--title "%islik%"]
              ensure-movie --tmdb-id 378435
              keyword-graph-reconcile
              keyword-graph-verify-readiness
              mdb-keywords-movie --tmdb-id <id>
              mdb-keywords-tv --tmdb-id <id>
              mdb-keywords-sync-movie --tmdb-id <id>
              mdb-keywords-sync-tv --tmdb-id <id>

            Configuration: appsettings.json + environment (PostgreSql__*, Tmdb__ApiKey, MDBList__ApiKey).
            Keyword graph ops are explicit one-shot maintenance (no Hangfire / no API).
            """);
    }

    private static ParsedOptions ParseOptions(string[] args)
    {
        var options = new ParsedOptions();
        for (var index = 0; index < args.Length; index++)
        {
            var token = args[index];
            switch (token)
            {
                case "--max-items" when index + 1 < args.Length:
                    options.MaxItems = int.Parse(args[++index]);
                    break;
                case "--batch-size" when index + 1 < args.Length:
                    options.BatchSize = int.Parse(args[++index]);
                    break;
                case "--delay-ms" when index + 1 < args.Length:
                    options.DelayMs = int.Parse(args[++index]);
                    break;
                case "--content-type" when index + 1 < args.Length:
                    options.ContentType = Enum.Parse<CatalogContentType>(args[++index], ignoreCase: true);
                    break;
                case "--only-movie-id" when index + 1 < args.Length:
                    options.OnlyMovieId = Guid.Parse(args[++index]);
                    break;
                case "--only-tv-id" when index + 1 < args.Length:
                    options.OnlyTvShowId = Guid.Parse(args[++index]);
                    break;
                case "--after-movie-id" when index + 1 < args.Length:
                    options.StartAfterMovieId = Guid.Parse(args[++index]);
                    break;
                case "--after-tv-id" when index + 1 < args.Length:
                    options.StartAfterTvShowId = Guid.Parse(args[++index]);
                    break;
                case "--title" when index + 1 < args.Length:
                    options.Title = args[++index];
                    break;
                case "--tmdb-id" when index + 1 < args.Length:
                    options.TmdbId = int.Parse(args[++index]);
                    break;
                case "--query" when index + 1 < args.Length:
                    options.Queries.Add(args[++index]);
                    break;
                case "--needs-search-title-metadata-repair":
                    options.NeedsSearchTitleMetadataRepair = true;
                    break;
            }
        }

        return options;
    }

    private sealed class ParsedOptions
    {
        public int? MaxItems { get; set; }

        public int? BatchSize { get; set; }

        public int? DelayMs { get; set; }

        public CatalogContentType? ContentType { get; set; }

        public Guid? OnlyMovieId { get; set; }

        public Guid? OnlyTvShowId { get; set; }

        public Guid? StartAfterMovieId { get; set; }

        public Guid? StartAfterTvShowId { get; set; }

        public string? Title { get; set; }

        public int? TmdbId { get; set; }

        public List<string> Queries { get; } = [];

        public bool NeedsSearchTitleMetadataRepair { get; set; }
    }
}
