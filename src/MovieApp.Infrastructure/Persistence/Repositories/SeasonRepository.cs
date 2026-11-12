using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class SeasonRepository(ApplicationDbContext dbContext) : ISeasonRepository
{
    public async Task<Season?> GetByTvShowIdAndSeasonNumberAsync(
        Guid tvShowId,
        int seasonNumber,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Seasons
            .AsNoTracking()
            .Include(season => season.Episodes)
            .FirstOrDefaultAsync(
                season => season.TvShowId == tvShowId && season.SeasonNumber == seasonNumber,
                cancellationToken);
    }

    public async Task<IReadOnlySet<int>> GetRegularSeasonNumbersWithEpisodesAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var seasonNumbers = await dbContext.Seasons
            .AsNoTracking()
            .Where(season => season.TvShowId == tvShowId && season.SeasonNumber >= 1)
            .Where(season => season.Episodes.Any())
            .Select(season => season.SeasonNumber)
            .ToListAsync(cancellationToken);

        return seasonNumbers.ToHashSet();
    }

    public async Task<bool> IsRegularEpisodeIngestionRequiredAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default) =>
        (await CheckRegularEpisodeIngestionRequiredAsync(tvShowId, cancellationToken)).IsRequired;

    public async Task<RegularEpisodeIngestionCheckResult> CheckRegularEpisodeIngestionRequiredAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var metadataStopwatch = Stopwatch.StartNew();
        var regularSeasons = await dbContext.Seasons
            .AsNoTracking()
            .Where(season => season.TvShowId == tvShowId && season.SeasonNumber >= 1)
            .Select(season => new { season.SeasonNumber, season.EpisodeCount })
            .ToListAsync(cancellationToken);
        metadataStopwatch.Stop();

        if (regularSeasons.Count == 0)
        {
            return new RegularEpisodeIngestionCheckResult(
                IsRequired: true,
                CatalogMetadataQueryMs: metadataStopwatch.ElapsedMilliseconds,
                SeasonsWithEpisodesQueryMs: 0,
                RegularSeasonCount: 0,
                SeasonsWithEpisodeRowsCount: 0,
                SeasonsMissingEpisodesCount: 0,
                MissingSeasonNumbers: []);
        }

        var episodesStopwatch = Stopwatch.StartNew();
        var seasonsWithEpisodes = await GetRegularSeasonNumbersWithEpisodesAsync(tvShowId, cancellationToken);
        episodesStopwatch.Stop();

        var missingSeasonNumbers = regularSeasons
            .Where(season => season.EpisodeCount != 0 && !seasonsWithEpisodes.Contains(season.SeasonNumber))
            .Select(season => season.SeasonNumber)
            .OrderBy(seasonNumber => seasonNumber)
            .ToList();

        return new RegularEpisodeIngestionCheckResult(
            IsRequired: missingSeasonNumbers.Count > 0,
            CatalogMetadataQueryMs: metadataStopwatch.ElapsedMilliseconds,
            SeasonsWithEpisodesQueryMs: episodesStopwatch.ElapsedMilliseconds,
            RegularSeasonCount: regularSeasons.Count,
            SeasonsWithEpisodeRowsCount: seasonsWithEpisodes.Count,
            SeasonsMissingEpisodesCount: missingSeasonNumbers.Count,
            MissingSeasonNumbers: missingSeasonNumbers);
    }

    public Task<Season> UpsertFromProviderAsync(
        Guid tvShowId,
        SeasonProviderDetails details,
        CancellationToken cancellationToken = default) =>
        ExecuteCatalogMutationAsync(
            tvShowId,
            async ct =>
            {
                var season = await LoadTrackedSeasonAsync(tvShowId, details.SeasonNumber, ct);
                var utcNow = DateTime.UtcNow;
                season = await ApplyProviderDetailsAsync(season, tvShowId, details, utcNow, ct);
                await dbContext.SaveChangesAsync(ct);
                return season;
            },
            cancellationToken);

    public Task<SeasonBatchUpsertPersistenceMetrics> UpsertSeasonsFromProviderAsync(
        Guid tvShowId,
        IReadOnlyList<SeasonProviderDetails> details,
        CancellationToken cancellationToken = default)
    {
        if (details.Count == 0)
        {
            return Task.FromResult(EmptyBatchUpsertMetrics());
        }

        var dedupedDetails = DeduplicateSeasonDetails(details);

        return ExecuteCatalogMutationAsync(
            tvShowId,
            async ct =>
            {
                var totalStopwatch = Stopwatch.StartNew();
                var existingDataLoadMs = 0L;
                var mutationPreparationMs = 0L;
                var saveChangesMs = 0L;

                var loadStopwatch = Stopwatch.StartNew();
                var seasonNumbers = dedupedDetails
                    .Select(item => item.SeasonNumber)
                    .Distinct()
                    .ToList();

                var existingSeasons = await dbContext.Seasons
                    .Where(season => season.TvShowId == tvShowId && seasonNumbers.Contains(season.SeasonNumber))
                    .ToListAsync(ct);

                await AttachAllEpisodesForSeasonsAsync(existingSeasons, ct);
                loadStopwatch.Stop();
                existingDataLoadMs = loadStopwatch.ElapsedMilliseconds;

                var seasonsByNumber = existingSeasons.ToDictionary(season => season.SeasonNumber);
                var utcNow = DateTime.UtcNow;
                var incomingEpisodeCount = dedupedDetails.Sum(item => DeduplicateEpisodeDetails(item.Episodes).Count);

                var mutationStopwatch = Stopwatch.StartNew();
                var autoDetectChanges = dbContext.ChangeTracker.AutoDetectChangesEnabled;
                dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
                try
                {
                    foreach (var seasonDetails in dedupedDetails)
                    {
                        if (!seasonsByNumber.TryGetValue(seasonDetails.SeasonNumber, out var season))
                        {
                            season = new Season
                            {
                                Id = Guid.NewGuid(),
                                TvShowId = tvShowId,
                                CreatedAt = utcNow
                            };

                            dbContext.Seasons.Add(season);
                            seasonsByNumber[seasonDetails.SeasonNumber] = season;
                        }

                        seasonsByNumber[seasonDetails.SeasonNumber] =
                            await ApplyProviderDetailsAsync(
                                season,
                                tvShowId,
                                seasonDetails,
                                utcNow,
                                ct,
                                resolveMissingEpisodesIndividually: false);
                    }

                    dbContext.ChangeTracker.DetectChanges();
                }
                finally
                {
                    dbContext.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
                }

                mutationStopwatch.Stop();
                mutationPreparationMs = mutationStopwatch.ElapsedMilliseconds;

                var (addedSeasonCount, updatedSeasonCount, addedEpisodeCount, updatedEpisodeCount) =
                    CountPendingSeasonEpisodeChanges();

                var saveStopwatch = Stopwatch.StartNew();
                await dbContext.SaveChangesAsync(ct);
                saveStopwatch.Stop();
                saveChangesMs = saveStopwatch.ElapsedMilliseconds;

                totalStopwatch.Stop();

                return new SeasonBatchUpsertPersistenceMetrics(
                    TotalMs: totalStopwatch.ElapsedMilliseconds,
                    AdvisoryLockMs: 0,
                    ExistingDataLoadMs: existingDataLoadMs,
                    MutationPreparationMs: mutationPreparationMs,
                    SaveChangesMs: saveChangesMs,
                    SeasonCount: dedupedDetails.Count,
                    IncomingEpisodeCount: incomingEpisodeCount,
                    AddedEpisodeCount: addedEpisodeCount,
                    UpdatedEpisodeCount: updatedEpisodeCount,
                    AddedSeasonCount: addedSeasonCount,
                    UpdatedSeasonCount: updatedSeasonCount);
            },
            cancellationToken);
    }

    private static SeasonBatchUpsertPersistenceMetrics EmptyBatchUpsertMetrics() =>
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public async Task<Season> UpsertSummaryFromProviderAsync(
        Guid tvShowId,
        SeasonProviderSummary summary,
        CancellationToken cancellationToken = default)
    {
        var season = await dbContext.Seasons
            .FirstOrDefaultAsync(
                existingSeason =>
                    existingSeason.TvShowId == tvShowId &&
                    existingSeason.SeasonNumber == summary.SeasonNumber,
                cancellationToken);

        var utcNow = DateTime.UtcNow;

        if (season is null)
        {
            season = new Season
            {
                Id = Guid.NewGuid(),
                TvShowId = tvShowId,
                CreatedAt = utcNow
            };

            dbContext.Seasons.Add(season);
        }

        season.SeasonNumber = summary.SeasonNumber;
        season.Name = summary.Name;
        season.AirDate = summary.AirDate;
        season.EpisodeCount = summary.EpisodeCount;
        season.PosterPath = summary.PosterPath;
        season.UpdatedAt = utcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return season;
    }

    private async Task<TResult> ExecuteCatalogMutationAsync<TResult>(
        Guid tvShowId,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        if (!IsPostgreSql())
        {
            return await action(cancellationToken);
        }

        if (dbContext.Database.CurrentTransaction is not null)
        {
            await AcquireCatalogHydrationLockAsync(tvShowId, cancellationToken);
            return await action(cancellationToken);
        }

        return await dbContext.Database.ExecuteInRetriableTransactionAsync(
            async ct =>
            {
                await AcquireCatalogHydrationLockAsync(tvShowId, ct);
                return await action(ct);
            },
            cancellationToken);
    }

    private async Task ExecuteCatalogMutationAsync(
        Guid tvShowId,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken) =>
        await ExecuteCatalogMutationAsync(
            tvShowId,
            async ct =>
            {
                await action(ct);
                return true;
            },
            cancellationToken);

    private async Task AcquireCatalogHydrationLockAsync(
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        var lockKey = TvShowCatalogLockKeys.ForCatalogHydration(tvShowId);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey})",
            cancellationToken);
    }

    private bool IsPostgreSql() =>
        dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    private async Task<Season?> LoadTrackedSeasonAsync(
        Guid tvShowId,
        int seasonNumber,
        CancellationToken cancellationToken)
    {
        return await dbContext.Seasons
            .Include(existingSeason => existingSeason.Episodes)
            .FirstOrDefaultAsync(
                existingSeason =>
                    existingSeason.TvShowId == tvShowId &&
                    existingSeason.SeasonNumber == seasonNumber,
                cancellationToken);
    }

    private async Task<Season> ApplyProviderDetailsAsync(
        Season? season,
        Guid tvShowId,
        SeasonProviderDetails details,
        DateTime utcNow,
        CancellationToken cancellationToken,
        bool resolveMissingEpisodesIndividually = true)
    {
        if (season is null)
        {
            season = new Season
            {
                Id = Guid.NewGuid(),
                TvShowId = tvShowId,
                CreatedAt = utcNow
            };

            dbContext.Seasons.Add(season);
        }

        season.TmdbId = details.TmdbId;
        season.TvdbId = details.TvdbId;
        season.SeasonNumber = details.SeasonNumber;
        season.Name = details.Name;
        season.Overview = details.Overview;
        season.AirDate = details.AirDate;
        season.EpisodeCount = details.EpisodeCount;
        season.PosterPath = details.PosterPath;
        season.UpdatedAt = utcNow;

        var episodeDetails = DeduplicateEpisodeDetails(details.Episodes);
        var episodesByNumber = BuildEpisodeIndex(season);
        if (resolveMissingEpisodesIndividually)
        {
            await LoadMissingEpisodesAsync(season, episodeDetails, episodesByNumber, cancellationToken);
        }

        // TMDB IDs identify logical episodes. Prefer the existing row that owns an incoming
        // TMDB ID, even when the provider has changed its episode number. Fall back to the
        // current episode-number match for episodes whose TMDB ID is new or unavailable.
        episodesByNumber = BuildProviderEpisodeIndex(season, episodeDetails);
        await MoveReassignedEpisodeNumbersToTemporaryValuesAsync(episodesByNumber, episodeDetails, cancellationToken);
        await ClearReassignedEpisodeTmdbIdsAsync(season, episodeDetails, cancellationToken);

        foreach (var episodeDetail in episodeDetails)
        {
            UpsertLoadedEpisode(season, episodeDetail, utcNow, episodesByNumber);
        }

        return season;
    }

    private static Dictionary<int, Episode> BuildEpisodeIndex(Season season)
    {
        var episodesByNumber = new Dictionary<int, Episode>();

        foreach (var episode in season.Episodes)
        {
            episodesByNumber.TryAdd(episode.EpisodeNumber, episode);
        }

        return episodesByNumber;
    }

    private static Dictionary<int, Episode> BuildProviderEpisodeIndex(
        Season season,
        IReadOnlyList<EpisodeProviderDetails> episodeDetails)
    {
        var existingByTmdbId = season.Episodes
            .Where(episode => episode.TmdbId.HasValue)
            .GroupBy(episode => episode.TmdbId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var existingByNumber = BuildEpisodeIndex(season);
        var result = new Dictionary<int, Episode>();
        var assignedEpisodeIds = new HashSet<Guid>();

        foreach (var details in episodeDetails)
        {
            if (details.TmdbId.HasValue &&
                existingByTmdbId.TryGetValue(details.TmdbId.Value, out var tmdbMatch))
            {
                result[details.EpisodeNumber] = tmdbMatch;
                assignedEpisodeIds.Add(tmdbMatch.Id);
            }
        }

        foreach (var details in episodeDetails)
        {
            if (result.ContainsKey(details.EpisodeNumber))
            {
                continue;
            }

            if (existingByNumber.TryGetValue(details.EpisodeNumber, out var numberMatch) &&
                assignedEpisodeIds.Add(numberMatch.Id))
            {
                result[details.EpisodeNumber] = numberMatch;
            }
        }

        return result;
    }

    private async Task LoadMissingEpisodesAsync(
        Season season,
        IReadOnlyList<EpisodeProviderDetails> episodeDetails,
        Dictionary<int, Episode> episodesByNumber,
        CancellationToken cancellationToken)
    {
        if (dbContext.Entry(season).State == EntityState.Added)
        {
            return;
        }

        var missingNumbers = episodeDetails
            .Select(episode => episode.EpisodeNumber)
            .Where(episodeNumber => !episodesByNumber.ContainsKey(episodeNumber))
            .Distinct()
            .ToList();

        if (missingNumbers.Count == 0)
        {
            return;
        }

        var existingEpisodes = await dbContext.Episodes
            .Where(episode => episode.SeasonId == season.Id && missingNumbers.Contains(episode.EpisodeNumber))
            .ToListAsync(cancellationToken);

        foreach (var episode in existingEpisodes)
        {
            episodesByNumber[episode.EpisodeNumber] = episode;
            if (!season.Episodes.Any(item => item.Id == episode.Id))
            {
                season.Episodes.Add(episode);
            }
        }
    }

    private async Task MoveReassignedEpisodeNumbersToTemporaryValuesAsync(
        Dictionary<int, Episode> episodesByNumber,
        IReadOnlyList<EpisodeProviderDetails> episodeDetails,
        CancellationToken cancellationToken)
    {
        var reassignedEpisodes = episodeDetails
            .Where(details =>
                episodesByNumber.TryGetValue(details.EpisodeNumber, out var episode) &&
                episode.EpisodeNumber != details.EpisodeNumber)
            .Select(details =>
            {
                var episode = episodesByNumber[details.EpisodeNumber];
                return (Episode: episode, OriginalNumber: episode.EpisodeNumber);
            })
            .ToList();

        if (reassignedEpisodes.Count == 0)
        {
            return;
        }

        var episodeIds = reassignedEpisodes
            .Select(item => item.Episode.Id)
            .ToList();

        // The (SeasonId, EpisodeNumber) unique index is immediate. Move only the
        // rows whose numbers are changing to temporary negative values so EF can
        // persist swaps without building a circular unique-index dependency graph.
        await dbContext.Episodes
            .Where(episode => episodeIds.Contains(episode.Id))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(episode => episode.EpisodeNumber, episode => -episode.EpisodeNumber),
                cancellationToken);

        foreach (var (episode, originalNumber) in reassignedEpisodes)
        {
            dbContext.Entry(episode)
                .Property(item => item.EpisodeNumber)
                .OriginalValue = -originalNumber;
        }
    }

    private async Task ClearReassignedEpisodeTmdbIdsAsync(
        Season season,
        IReadOnlyList<EpisodeProviderDetails> episodeDetails,
        CancellationToken cancellationToken)
    {
        var duplicateIncomingTmdbIds = episodeDetails
            .Where(episode => episode.TmdbId.HasValue)
            .GroupBy(episode => episode.TmdbId!.Value)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateIncomingTmdbIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Provider returned duplicate TMDB episode IDs for season {season.SeasonNumber}: " +
                string.Join(", ", duplicateIncomingTmdbIds));
        }

        var incomingByEpisodeNumber = episodeDetails.ToDictionary(episode => episode.EpisodeNumber);
        var existingByTmdbId = season.Episodes
            .Where(episode => episode.TmdbId.HasValue)
            .GroupBy(episode => episode.TmdbId!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        var episodesToRelease = new HashSet<Episode>();
        foreach (var incomingEpisode in episodeDetails)
        {
            if (!incomingEpisode.TmdbId.HasValue ||
                !existingByTmdbId.TryGetValue(incomingEpisode.TmdbId.Value, out var currentOwner) ||
                currentOwner.EpisodeNumber == incomingEpisode.EpisodeNumber)
            {
                continue;
            }

            if (!incomingByEpisodeNumber.TryGetValue(currentOwner.EpisodeNumber, out var ownerReplacement) ||
                ownerReplacement.TmdbId == currentOwner.TmdbId)
            {
                throw new InvalidOperationException(
                    $"TMDB episode ID {incomingEpisode.TmdbId.Value} is already assigned to episode " +
                    $"{currentOwner.EpisodeNumber} in season {season.SeasonNumber}, and the provider payload " +
                    "does not reassign that existing episode.");
            }

            episodesToRelease.Add(currentOwner);
        }

        if (episodesToRelease.Count == 0)
        {
            return;
        }

        var episodeIdsToRelease = episodesToRelease
            .Select(episode => episode.Id)
            .ToList();

        // Free all IDs involved in a provider-side episode renumbering/swap before assigning
        // their new owners; the unique index is immediate, so a single UPDATE batch can fail.
        // ExecuteUpdate keeps this as one targeted SQL statement and leaves tracked entities ready
        // to receive their replacement IDs in the normal SaveChanges call.
        await dbContext.Episodes
            .Where(episode => episodeIdsToRelease.Contains(episode.Id))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(episode => episode.TmdbId, (int?)null),
                cancellationToken);

        // ExecuteUpdate bypasses the change tracker. Mark the tracked original values as null
        // so the incoming TMDB IDs assigned by ApplyEpisodeDetails are persisted by SaveChanges.
        foreach (var episode in episodesToRelease)
        {
            dbContext.Entry(episode)
                .Property(item => item.TmdbId)
                .OriginalValue = null;
        }
    }

    private void UpsertLoadedEpisode(
        Season season,
        EpisodeProviderDetails details,
        DateTime utcNow,
        Dictionary<int, Episode> episodesByNumber)
    {
        if (!episodesByNumber.TryGetValue(details.EpisodeNumber, out var episode))
        {
            episode = new Episode
            {
                Id = Guid.NewGuid(),
                SeasonId = season.Id,
                Season = season,
                CreatedAt = utcNow
            };

            season.Episodes.Add(episode);
            dbContext.Episodes.Add(episode);
            episodesByNumber[details.EpisodeNumber] = episode;
        }

        ApplyEpisodeDetails(episode, details, utcNow);
    }

    private static void ApplyEpisodeDetails(
        Episode episode,
        EpisodeProviderDetails details,
        DateTime utcNow)
    {
        episode.TmdbId = details.TmdbId;
        episode.TvdbId = details.TvdbId;
        episode.ImdbId = details.ImdbId;
        episode.EpisodeNumber = details.EpisodeNumber;
        episode.Name = details.Name;
        episode.Overview = details.Overview;
        episode.AirDate = details.AirDate;
        episode.RuntimeMinutes = details.RuntimeMinutes;
        episode.StillPath = details.StillPath;
        episode.VoteAverage = details.VoteAverage;
        episode.VoteCount = details.VoteCount;
        episode.UpdatedAt = utcNow;
    }

    private static List<SeasonProviderDetails> DeduplicateSeasonDetails(
        IReadOnlyList<SeasonProviderDetails> details) =>
        details
            .GroupBy(item => item.SeasonNumber)
            .Select(group => group.Last())
            .ToList();

    private static List<EpisodeProviderDetails> DeduplicateEpisodeDetails(
        IReadOnlyList<EpisodeProviderDetails> episodes) =>
        episodes
            .GroupBy(item => item.EpisodeNumber)
            .Select(group => group.Last())
            .ToList();

    private async Task AttachAllEpisodesForSeasonsAsync(
        IReadOnlyList<Season> seasons,
        CancellationToken cancellationToken)
    {
        var persistedSeasons = seasons
            .Where(season => dbContext.Entry(season).State != EntityState.Added)
            .ToList();

        if (persistedSeasons.Count == 0)
        {
            return;
        }

        var seasonIds = persistedSeasons.Select(season => season.Id).ToList();
        var episodes = await dbContext.Episodes
            .Where(episode => seasonIds.Contains(episode.SeasonId))
            .ToListAsync(cancellationToken);

        var seasonsById = persistedSeasons.ToDictionary(season => season.Id);
        foreach (var episode in episodes)
        {
            if (!seasonsById.TryGetValue(episode.SeasonId, out var season))
            {
                continue;
            }

            if (season.Episodes.Any(existing => existing.Id == episode.Id))
            {
                continue;
            }

            season.Episodes.Add(episode);
        }
    }

    private (int AddedSeasonCount, int UpdatedSeasonCount, int AddedEpisodeCount, int UpdatedEpisodeCount)
        CountPendingSeasonEpisodeChanges()
    {
        var addedSeasonCount = 0;
        var updatedSeasonCount = 0;
        var addedEpisodeCount = 0;
        var updatedEpisodeCount = 0;

        foreach (var entry in dbContext.ChangeTracker.Entries<Season>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    addedSeasonCount++;
                    break;
                case EntityState.Modified:
                    updatedSeasonCount++;
                    break;
            }
        }

        foreach (var entry in dbContext.ChangeTracker.Entries<Episode>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    addedEpisodeCount++;
                    break;
                case EntityState.Modified:
                    updatedEpisodeCount++;
                    break;
            }
        }

        return (addedSeasonCount, updatedSeasonCount, addedEpisodeCount, updatedEpisodeCount);
    }
}
