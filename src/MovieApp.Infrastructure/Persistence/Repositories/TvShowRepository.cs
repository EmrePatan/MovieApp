using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class TvShowRepository(ApplicationDbContext dbContext) : ITvShowRepository
{
    public async Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.TvShows
            .AsNoTracking()
            .AsSplitQuery()
            .Include(tvShow => tvShow.TvShowGenres)
            .ThenInclude(tvShowGenre => tvShowGenre.Genre)
            .Include(tvShow => tvShow.Seasons)
            .FirstOrDefaultAsync(tvShow => tvShow.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, TvShow>> GetByIdsAsync(
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, TvShow>();
        }

        var tvShows = await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => ids.Contains(tvShow.Id))
            .ToListAsync(cancellationToken);

        return tvShows.ToDictionary(tvShow => tvShow.Id);
    }

    public async Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default)
    {
        return await dbContext.TvShows
            .Include(tvShow => tvShow.TvShowGenres)
            .ThenInclude(tvShowGenre => tvShowGenre.Genre)
            .FirstOrDefaultAsync(tvShow => tvShow.TmdbId == tmdbId, cancellationToken);
    }

    public async Task<TvShow> UpsertFromProviderAsync(
        TvShowProviderDetails details,
        CancellationToken cancellationToken = default)
    {
        TvShow? tvShow = null;

        if (details.TmdbId.HasValue)
        {
            tvShow = await dbContext.TvShows
                .AsSplitQuery()
                .Include(existingTvShow => existingTvShow.TvShowGenres)
                .ThenInclude(tvShowGenre => tvShowGenre.Genre)
                .Include(existingTvShow => existingTvShow.Seasons)
                .FirstOrDefaultAsync(
                    existingTvShow => existingTvShow.TmdbId == details.TmdbId,
                    cancellationToken);
        }

        var utcNow = DateTime.UtcNow;

        if (tvShow is null)
        {
            tvShow = new TvShow
            {
                Id = Guid.NewGuid(),
                CreatedAt = utcNow
            };

            dbContext.TvShows.Add(tvShow);
        }

        ApplyProviderDetails(tvShow, details, utcNow);

        await SyncGenresAsync(tvShow, details.Genres, cancellationToken);
        await SyncSeasonSummariesAsync(tvShow, details.Seasons, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return tvShow;
    }

    public async Task<IReadOnlyList<TvShow>> UpsertFromProviderBatchAsync(
        IReadOnlyList<TvShowProviderDetails> details,
        CancellationToken cancellationToken = default)
    {
        if (details.Count == 0)
        {
            return [];
        }

        var tmdbIds = details
            .Where(detail => detail.TmdbId.HasValue)
            .Select(detail => detail.TmdbId!.Value)
            .Distinct()
            .ToList();

        var existingTvShows = tmdbIds.Count == 0
            ? []
            : await dbContext.TvShows
                .AsSplitQuery()
                .Include(tvShow => tvShow.TvShowGenres)
                .ThenInclude(tvShowGenre => tvShowGenre.Genre)
                .Include(tvShow => tvShow.Seasons)
                .Where(tvShow => tvShow.TmdbId.HasValue && tmdbIds.Contains(tvShow.TmdbId.Value))
                .ToListAsync(cancellationToken);

        var tvShowsByTmdbId = existingTvShows
            .Where(tvShow => tvShow.TmdbId.HasValue)
            .ToDictionary(tvShow => tvShow.TmdbId!.Value);

        var genresByName = await LoadGenresByNameAsync(
            details.SelectMany(detail => detail.Genres),
            cancellationToken);

        var utcNow = DateTime.UtcNow;
        var results = new List<TvShow>(details.Count);

        foreach (var detail in details)
        {
            TvShow tvShow;

            if (detail.TmdbId.HasValue && tvShowsByTmdbId.TryGetValue(detail.TmdbId.Value, out var existingTvShow))
            {
                tvShow = existingTvShow;
            }
            else
            {
                tvShow = new TvShow
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = utcNow
                };

                dbContext.TvShows.Add(tvShow);

                if (detail.TmdbId.HasValue)
                {
                    tvShowsByTmdbId[detail.TmdbId.Value] = tvShow;
                }
            }

            ApplyProviderDetails(tvShow, detail, utcNow);
            SyncGenresWithContext(tvShow, detail.Genres, genresByName, utcNow);
            SyncSeasonSummaries(tvShow, detail.Seasons, utcNow);
            results.Add(tvShow);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return results;
    }

    public async Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
        IReadOnlyList<int> tmdbIds,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = tmdbIds
            .Where(tmdbId => tmdbId > 0)
            .Distinct()
            .ToList();

        if (distinctIds.Count == 0)
        {
            return new Dictionary<int, Guid>();
        }

        return await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShow.TmdbId.HasValue && distinctIds.Contains(tvShow.TmdbId.Value))
            .Select(tvShow => new { tvShow.TmdbId, tvShow.Id })
            .ToDictionaryAsync(
                tvShow => tvShow.TmdbId!.Value,
                tvShow => tvShow.Id,
                cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
        IReadOnlyList<TvShowProviderSummary> summaries,
        CancellationToken cancellationToken = default)
    {
        var tmdbIds = summaries
            .Where(summary => summary.TmdbId.HasValue)
            .Select(summary => summary.TmdbId!.Value)
            .Distinct()
            .ToList();

        if (tmdbIds.Count == 0)
        {
            return new Dictionary<int, Guid>();
        }

        var existingIds = await GetExistingIdsByTmdbIdsAsync(tmdbIds, cancellationToken);
        var mutableExistingIds = existingIds.ToDictionary(pair => pair.Key, pair => pair.Value);

        var utcNow = DateTime.UtcNow;
        var hasChanges = false;

        foreach (var summary in summaries)
        {
            if (!summary.TmdbId.HasValue || mutableExistingIds.ContainsKey(summary.TmdbId.Value))
            {
                continue;
            }

            var tvShow = new TvShow
            {
                Id = Guid.NewGuid(),
                TmdbId = summary.TmdbId,
                TvdbId = summary.TvdbId,
                ImdbId = summary.ImdbId,
                Title = summary.Title,
                OriginalTitle = summary.OriginalTitle,
                Overview = summary.Overview,
                FirstAirDate = summary.FirstAirDate,
                PosterPath = summary.PosterPath,
                BackdropPath = summary.BackdropPath,
                OriginalLanguage = summary.OriginalLanguage,
                VoteAverage = summary.VoteAverage,
                VoteCount = summary.VoteCount,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

            dbContext.TvShows.Add(tvShow);
            mutableExistingIds[summary.TmdbId.Value] = tvShow.Id;
            hasChanges = true;
        }

        if (!hasChanges)
        {
            return mutableExistingIds;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return mutableExistingIds;
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionExtensions.IsUniqueConstraintViolation(exception))
        {
            return await GetExistingIdsByTmdbIdsAsync(tmdbIds, cancellationToken);
        }
    }

    private async Task SyncGenresAsync(
        TvShow tvShow,
        IReadOnlyList<string> genreNames,
        CancellationToken cancellationToken)
    {
        var genresByName = await LoadGenresByNameAsync(genreNames, cancellationToken);
        SyncGenresWithContext(tvShow, genreNames, genresByName, DateTime.UtcNow);
    }

    private async Task<Dictionary<string, Genre>> LoadGenresByNameAsync(
        IEnumerable<string> genreNames,
        CancellationToken cancellationToken)
    {
        var normalizedGenreNames = genreNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedGenreNames.Count == 0)
        {
            return new Dictionary<string, Genre>(StringComparer.OrdinalIgnoreCase);
        }

        var existingGenres = await dbContext.Genres
            .Where(genre => normalizedGenreNames.Contains(genre.Name))
            .ToListAsync(cancellationToken);

        return existingGenres.ToDictionary(
            genre => genre.Name,
            StringComparer.OrdinalIgnoreCase);
    }

    private static void ApplyProviderDetails(TvShow tvShow, TvShowProviderDetails details, DateTime utcNow)
    {
        tvShow.TmdbId = details.TmdbId;
        tvShow.TvdbId = details.TvdbId;
        tvShow.ImdbId = details.ImdbId;
        tvShow.Title = details.Title;
        tvShow.OriginalTitle = details.OriginalTitle;
        tvShow.Overview = details.Overview;
        tvShow.FirstAirDate = details.FirstAirDate;
        tvShow.LastAirDate = details.LastAirDate;
        tvShow.PosterPath = details.PosterPath;
        tvShow.BackdropPath = details.BackdropPath;
        tvShow.OriginalLanguage = details.OriginalLanguage;
        tvShow.VoteAverage = details.VoteAverage;
        tvShow.VoteCount = details.VoteCount;
        tvShow.Status = TvShowStatusParser.Parse(details.Status);
        tvShow.UpdatedAt = utcNow;
    }

    private void SyncGenresWithContext(
        TvShow tvShow,
        IReadOnlyList<string> genreNames,
        Dictionary<string, Genre> genresByName,
        DateTime utcNow)
    {
        var normalizedGenreNames = genreNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var linkedGenreIds = new HashSet<Guid>();

        foreach (var genreName in normalizedGenreNames)
        {
            if (!genresByName.TryGetValue(genreName, out var genre))
            {
                genre = new Genre
                {
                    Id = Guid.NewGuid(),
                    Name = genreName,
                    CreatedAt = utcNow
                };

                dbContext.Genres.Add(genre);
                genresByName[genreName] = genre;
            }

            linkedGenreIds.Add(genre.Id);

            var alreadyLinked = tvShow.TvShowGenres.Any(tvShowGenre => tvShowGenre.GenreId == genre.Id);
            if (!alreadyLinked)
            {
                tvShow.TvShowGenres.Add(new TvShowGenre
                {
                    TvShowId = tvShow.Id,
                    GenreId = genre.Id,
                    Genre = genre,
                    TvShow = tvShow
                });
            }
        }

        var genresToRemove = tvShow.TvShowGenres
            .Where(tvShowGenre => !linkedGenreIds.Contains(tvShowGenre.GenreId))
            .ToList();

        foreach (var tvShowGenre in genresToRemove)
        {
            tvShow.TvShowGenres.Remove(tvShowGenre);
        }
    }

    private async Task SyncSeasonSummariesAsync(
        TvShow tvShow,
        IReadOnlyList<SeasonProviderSummary> seasons,
        CancellationToken cancellationToken)
    {
        SyncSeasonSummaries(tvShow, seasons, DateTime.UtcNow);
        await Task.CompletedTask;
    }

    private void SyncSeasonSummaries(
        TvShow tvShow,
        IReadOnlyList<SeasonProviderSummary> seasons,
        DateTime utcNow)
    {
        foreach (var summary in seasons)
        {
            var season = tvShow.Seasons.FirstOrDefault(item => item.SeasonNumber == summary.SeasonNumber);

            if (season is null)
            {
                season = new Season
                {
                    Id = Guid.NewGuid(),
                    TvShowId = tvShow.Id,
                    TvShow = tvShow,
                    CreatedAt = utcNow
                };

                tvShow.Seasons.Add(season);
                dbContext.Seasons.Add(season);
            }

            season.SeasonNumber = summary.SeasonNumber;
            season.Name = summary.Name;
            season.AirDate = summary.AirDate;
            season.EpisodeCount = summary.EpisodeCount;
            season.PosterPath = summary.PosterPath;
            season.UpdatedAt = utcNow;
        }
    }
}
