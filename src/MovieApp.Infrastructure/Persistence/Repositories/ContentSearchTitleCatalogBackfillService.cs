using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class ContentSearchTitleCatalogBackfillService(
    ApplicationDbContext dbContext,
    IContentSearchTitleSynchronizer synchronizer) : IContentSearchTitleCatalogBackfillService
{
    private const int BatchSize = 200;

    public async Task BackfillCanonicalAndOriginalAsync(CancellationToken cancellationToken = default)
    {
        var movieOffset = 0;
        while (true)
        {
            var movies = await dbContext.Movies
                .AsNoTracking()
                .OrderBy(movie => movie.Id)
                .Skip(movieOffset)
                .Take(BatchSize)
                .Select(movie => new { movie.Id, movie.Title, movie.OriginalTitle })
                .ToListAsync(cancellationToken);

            if (movies.Count == 0)
            {
                break;
            }

            foreach (var movie in movies)
            {
                await synchronizer.SyncCatalogTitlesAsync(
                    CatalogContentType.Movie,
                    movie.Id,
                    movie.Title,
                    movie.OriginalTitle,
                    cancellationToken);
            }

            movieOffset += movies.Count;
        }

        var tvOffset = 0;
        while (true)
        {
            var tvShows = await dbContext.TvShows
                .AsNoTracking()
                .OrderBy(tvShow => tvShow.Id)
                .Skip(tvOffset)
                .Take(BatchSize)
                .Select(tvShow => new { tvShow.Id, tvShow.Title, tvShow.OriginalTitle })
                .ToListAsync(cancellationToken);

            if (tvShows.Count == 0)
            {
                break;
            }

            foreach (var tvShow in tvShows)
            {
                await synchronizer.SyncCatalogTitlesAsync(
                    CatalogContentType.Tv,
                    tvShow.Id,
                    tvShow.Title,
                    tvShow.OriginalTitle,
                    cancellationToken);
            }

            tvOffset += tvShows.Count;
        }
    }
}
