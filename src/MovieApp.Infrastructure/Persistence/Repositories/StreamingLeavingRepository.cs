using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.StreamingLeaving;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

/// <summary>Uses its own context per call so a long fetch never shares tracked state with other work.</summary>
public sealed class StreamingLeavingRepository(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    : IStreamingLeavingRepository
{
    public async Task<StreamingLeavingRegion?> GetRegionAsync(
        string region,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.StreamingLeavingRegions.AsNoTracking()
            .FirstOrDefaultAsync(state => state.Region == region, cancellationToken);
    }

    public async Task SaveRegionAsync(StreamingLeavingRegion state, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var stored = await dbContext.StreamingLeavingRegions
            .FirstOrDefaultAsync(item => item.Region == state.Region, cancellationToken);
        if (stored is null)
        {
            stored = new StreamingLeavingRegion { Region = state.Region };
            dbContext.StreamingLeavingRegions.Add(stored);
        }

        dbContext.Entry(stored).CurrentValues.SetValues(state);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, int>> GetUsageAsync(
        DateOnly periodStart,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.StreamingApiUsage.AsNoTracking()
            .Where(usage => usage.PeriodStart == periodStart)
            .ToDictionaryAsync(usage => usage.Region, usage => usage.RequestCount, cancellationToken);
    }

    public async Task<bool> TryReserveRequestAsync(
        DateOnly periodStart,
        string region,
        Func<IReadOnlyDictionary<string, int>, bool> isAllowed,
        CancellationToken cancellationToken = default)
    {
        await using var strategyContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await strategyContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            // Held until commit: two regions cannot both take the last request.
            await dbContext.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(hashtext('streaming-api-usage'))",
                cancellationToken);

            var rows = await dbContext.StreamingApiUsage
                .Where(usage => usage.PeriodStart == periodStart)
                .ToListAsync(cancellationToken);
            if (!isAllowed(rows.ToDictionary(usage => usage.Region, usage => usage.RequestCount)))
            {
                return false;
            }

            var row = rows.Find(usage => usage.Region == region);
            if (row is null)
            {
                row = new StreamingApiUsage { PeriodStart = periodStart, Region = region };
                dbContext.StreamingApiUsage.Add(row);
            }

            row.RequestCount++;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }

    public async Task<IReadOnlyList<StreamingLeavingRow>> GetUpcomingAsync(
        string region,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await dbContext.StreamingLeavingItems.AsNoTracking()
            .Where(item => item.Region == region && item.LeavesAtUtc > nowUtc)
            .ToListAsync(cancellationToken);
        if (items.Count == 0)
        {
            return [];
        }

        var movieIds = items.Where(item => item.MediaType == CatalogContentType.Movie)
            .Select(item => item.ContentId).Distinct().ToList();
        var tvIds = items.Where(item => item.MediaType == CatalogContentType.Tv)
            .Select(item => item.ContentId).Distinct().ToList();

        var movies = await dbContext.Movies.AsNoTracking()
            .Where(movie => movieIds.Contains(movie.Id))
            .Select(movie => new
            {
                Title = new CatalogTitle(
                    movie.Id,
                    movie.Title,
                    movie.OriginalTitle,
                    movie.Overview,
                    movie.PosterPath,
                    movie.BackdropPath,
                    movie.ReleaseDate,
                    movie.RuntimeMinutes,
                    movie.OriginalLanguage,
                    movie.PrimaryOriginCountryCode,
                    movie.VoteAverage,
                    movie.VoteCount),
                GenreIds = movie.MovieGenres.Select(genre => genre.GenreId).ToList()
            })
            .ToListAsync(cancellationToken);
        var tvShows = await dbContext.TvShows.AsNoTracking()
            .Where(tvShow => tvIds.Contains(tvShow.Id))
            .Select(tvShow => new
            {
                Title = new CatalogTitle(
                    tvShow.Id,
                    tvShow.Title,
                    tvShow.OriginalTitle,
                    tvShow.Overview,
                    tvShow.PosterPath,
                    tvShow.BackdropPath,
                    tvShow.FirstAirDate,
                    null,
                    tvShow.OriginalLanguage,
                    tvShow.PrimaryOriginCountryCode,
                    tvShow.VoteAverage,
                    tvShow.VoteCount),
                GenreIds = tvShow.TvShowGenres.Select(genre => genre.GenreId).ToList()
            })
            .ToListAsync(cancellationToken);

        var titles = movies
            .Select(movie => (Type: CatalogContentType.Movie, movie.Title, movie.GenreIds))
            .Concat(tvShows.Select(tvShow => (Type: CatalogContentType.Tv, tvShow.Title, tvShow.GenreIds)))
            .ToDictionary(entry => (entry.Type, entry.Title.Id));

        var rows = new List<StreamingLeavingRow>(items.Count);
        foreach (var item in items)
        {
            // A title removed from the catalog since the fetch is left out.
            if (!titles.TryGetValue((item.MediaType, item.ContentId), out var entry))
            {
                continue;
            }

            var title = entry.Title;
            rows.Add(new StreamingLeavingRow(
                item.ServiceId,
                item.ServiceName,
                item.WatchLink,
                item.LeavesAtUtc,
                item.MediaType,
                item.ContentId,
                item.TmdbId,
                title.Name,
                title.OriginalTitle,
                title.Overview,
                title.PosterPath,
                title.BackdropPath,
                title.ReleaseDate,
                title.RuntimeMinutes,
                title.OriginalLanguage,
                title.OriginCountryCode,
                title.VoteAverage,
                title.VoteCount,
                entry.GenreIds));
        }

        return rows;
    }

    public async Task<bool> HasUpcomingAsync(
        string region,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.StreamingLeavingItems.AnyAsync(
            item => item.Region == region && item.LeavesAtUtc > nowUtc,
            cancellationToken);
    }

    public async Task ReplaceItemsAsync(
        string region,
        IReadOnlyList<StreamingLeavingItem> items,
        CancellationToken cancellationToken = default)
    {
        await using var strategyContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await strategyContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            await dbContext.StreamingLeavingItems
                .Where(item => item.Region == region)
                .ExecuteDeleteAsync(cancellationToken);
            dbContext.StreamingLeavingItems.AddRange(items);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private sealed record CatalogTitle(
        Guid Id,
        string Name,
        string? OriginalTitle,
        string? Overview,
        string? PosterPath,
        string? BackdropPath,
        DateOnly? ReleaseDate,
        int? RuntimeMinutes,
        string? OriginalLanguage,
        string? OriginCountryCode,
        decimal VoteAverage,
        int VoteCount);
}
