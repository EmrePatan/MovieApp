using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;

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
        var season = await dbContext.Seasons
            .Include(existingSeason => existingSeason.Episodes)
            .FirstOrDefaultAsync(
                existingSeason =>
                    existingSeason.TvShowId == tvShowId &&
                    existingSeason.SeasonNumber == details.SeasonNumber,
                cancellationToken);

        var utcNow = DateTime.UtcNow;
        season = ApplyProviderDetails(season, tvShowId, details, utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return season;
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

        var seasonNumbers = details
            .Select(item => item.SeasonNumber)
            .Distinct()
            .ToList();

        var existingSeasons = await dbContext.Seasons
            .Include(season => season.Episodes)
            .Where(season => season.TvShowId == tvShowId && seasonNumbers.Contains(season.SeasonNumber))
            .ToListAsync(cancellationToken);

        var seasonsByNumber = existingSeasons.ToDictionary(season => season.SeasonNumber);
        var utcNow = DateTime.UtcNow;

        foreach (var seasonDetails in details)
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
                ApplyProviderDetails(season, tvShowId, seasonDetails, utcNow);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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

    private Season ApplyProviderDetails(
        Season? season,
        Guid tvShowId,
        SeasonProviderDetails details,
        DateTime utcNow)
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

        foreach (var episodeDetails in details.Episodes)
        {
            UpsertEpisode(season, episodeDetails, utcNow);
        }

        return season;
    }

    private void UpsertEpisode(
        Season season,
        EpisodeProviderDetails details,
        DateTime utcNow)
    {
        var episode = season.Episodes.FirstOrDefault(item => item.EpisodeNumber == details.EpisodeNumber);

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
}
