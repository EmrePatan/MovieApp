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

    public async Task<Season> UpsertFromProviderAsync(
        Guid tvShowId,
        SeasonProviderDetails details,
        CancellationToken cancellationToken = default)
    {
        var transaction = await BeginCatalogHydrationTransactionAsync(tvShowId, cancellationToken);

        try
        {
            var season = await LoadTrackedSeasonAsync(tvShowId, details.SeasonNumber, cancellationToken);
            var utcNow = DateTime.UtcNow;
            season = await ApplyProviderDetailsAsync(season, tvShowId, details, utcNow, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await CommitCatalogHydrationTransactionAsync(transaction, cancellationToken);
            return season;
        }
        finally
        {
            await DisposeCatalogHydrationTransactionAsync(transaction);
        }
    }

    public async Task UpsertSeasonsFromProviderAsync(
        Guid tvShowId,
        IReadOnlyList<SeasonProviderDetails> details,
        CancellationToken cancellationToken = default)
    {
        if (details.Count == 0)
        {
            return;
        }

        var dedupedDetails = DeduplicateSeasonDetails(details);
        var transaction = await BeginCatalogHydrationTransactionAsync(tvShowId, cancellationToken);

        try
        {
        var seasonNumbers = dedupedDetails
            .Select(item => item.SeasonNumber)
            .Distinct()
            .ToList();

        var existingSeasons = await dbContext.Seasons
            .Include(season => season.Episodes)
            .Where(season => season.TvShowId == tvShowId && seasonNumbers.Contains(season.SeasonNumber))
            .ToListAsync(cancellationToken);

        var seasonsByNumber = existingSeasons.ToDictionary(season => season.SeasonNumber);
        var utcNow = DateTime.UtcNow;

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
                await ApplyProviderDetailsAsync(season, tvShowId, seasonDetails, utcNow, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitCatalogHydrationTransactionAsync(transaction, cancellationToken);
        }
        finally
        {
            await DisposeCatalogHydrationTransactionAsync(transaction);
        }
    }

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

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> BeginCatalogHydrationTransactionAsync(
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        if (!IsPostgreSql())
        {
            return null;
        }

        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var lockKey = TvShowCatalogLockKeys.ForCatalogHydration(tvShowId);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey})",
            cancellationToken);

        return transaction;
    }

    private static async Task CommitCatalogHydrationTransactionAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static async Task DisposeCatalogHydrationTransactionAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction)
    {
        if (transaction is not null)
        {
            await transaction.DisposeAsync();
        }
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
        CancellationToken cancellationToken)
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

        var episodesByNumber = BuildEpisodeIndex(season);
        foreach (var episodeDetails in DeduplicateEpisodeDetails(details.Episodes))
        {
            await UpsertEpisodeAsync(season, episodeDetails, utcNow, episodesByNumber, cancellationToken);
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

    private async Task UpsertEpisodeAsync(
        Season season,
        EpisodeProviderDetails details,
        DateTime utcNow,
        Dictionary<int, Episode> episodesByNumber,
        CancellationToken cancellationToken)
    {
        if (episodesByNumber.TryGetValue(details.EpisodeNumber, out var episode))
        {
            ApplyEpisodeDetails(episode, details, utcNow);
            return;
        }

        episode = await dbContext.Episodes
            .FirstOrDefaultAsync(
                existingEpisode =>
                    existingEpisode.SeasonId == season.Id &&
                    existingEpisode.EpisodeNumber == details.EpisodeNumber,
                cancellationToken);

        if (episode is null)
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
        }
        else if (!season.Episodes.Any(item => item.Id == episode.Id))
        {
            season.Episodes.Add(episode);
        }

        episodesByNumber[details.EpisodeNumber] = episode;
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
}
