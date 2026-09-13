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
            .Include(tvShow => tvShow.TvShowGenres)
            .ThenInclude(tvShowGenre => tvShowGenre.Genre)
            .Include(tvShow => tvShow.Seasons)
            .FirstOrDefaultAsync(tvShow => tvShow.Id == id, cancellationToken);
    }

    public async Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default)
    {
        return await dbContext.TvShows
            .Include(tvShow => tvShow.TvShowGenres)
            .ThenInclude(tvShowGenre => tvShowGenre.Genre)
            .Include(tvShow => tvShow.Seasons)
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

        await SyncGenresAsync(tvShow, details.Genres, cancellationToken);
        await SyncSeasonSummariesAsync(tvShow, details.Seasons, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return tvShow;
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

        var existingIds = await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShow.TmdbId.HasValue && tmdbIds.Contains(tvShow.TmdbId.Value))
            .Select(tvShow => new { tvShow.TmdbId, tvShow.Id })
            .ToDictionaryAsync(
                tvShow => tvShow.TmdbId!.Value,
                tvShow => tvShow.Id,
                cancellationToken);

        var utcNow = DateTime.UtcNow;
        var hasChanges = false;

        foreach (var summary in summaries)
        {
            if (!summary.TmdbId.HasValue || existingIds.ContainsKey(summary.TmdbId.Value))
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
            existingIds[summary.TmdbId.Value] = tvShow.Id;
            hasChanges = true;
        }

        if (!hasChanges)
        {
            return existingIds;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return existingIds;
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionExtensions.IsUniqueConstraintViolation(exception))
        {
            return await dbContext.TvShows
                .AsNoTracking()
                .Where(tvShow => tvShow.TmdbId.HasValue && tmdbIds.Contains(tvShow.TmdbId.Value))
                .Select(tvShow => new { tvShow.TmdbId, tvShow.Id })
                .ToDictionaryAsync(
                    tvShow => tvShow.TmdbId!.Value,
                    tvShow => tvShow.Id,
                    cancellationToken);
        }
    }

    private async Task SyncGenresAsync(
        TvShow tvShow,
        IReadOnlyList<string> genreNames,
        CancellationToken cancellationToken)
    {
        var normalizedGenreNames = genreNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingGenres = await dbContext.Genres
            .Where(genre => normalizedGenreNames.Contains(genre.Name))
            .ToListAsync(cancellationToken);

        var genresByName = existingGenres.ToDictionary(
            genre => genre.Name,
            StringComparer.OrdinalIgnoreCase);

        var utcNow = DateTime.UtcNow;
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
        foreach (var summary in seasons)
        {
            var season = tvShow.Seasons.FirstOrDefault(item => item.SeasonNumber == summary.SeasonNumber);
            var utcNow = DateTime.UtcNow;

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

        await Task.CompletedTask;
    }
}
