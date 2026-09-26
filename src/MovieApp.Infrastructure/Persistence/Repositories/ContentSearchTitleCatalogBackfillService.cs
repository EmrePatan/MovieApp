using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class ContentSearchTitleCatalogBackfillService(
    ApplicationDbContext dbContext,
    IContentSearchTitleSynchronizer synchronizer) : IContentSearchTitleCatalogBackfillService
{
    private const int BatchSize = 200;

    public async Task<ContentSearchTitleCatalogBackfillResult> BackfillCanonicalAndOriginalAsync(
        CancellationToken cancellationToken = default)
    {
        var moviesProcessed = 0;
        Guid? afterMovieId = null;

        while (true)
        {
            var movies = await dbContext.Movies
                .AsNoTracking()
                .Where(movie => afterMovieId == null || movie.Id.CompareTo(afterMovieId.Value) > 0)
                .OrderBy(movie => movie.Id)
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
                moviesProcessed++;
                afterMovieId = movie.Id;
            }
        }

        var tvShowsProcessed = 0;
        Guid? afterTvShowId = null;

        while (true)
        {
            var tvShows = await dbContext.TvShows
                .AsNoTracking()
                .Where(tvShow => afterTvShowId == null || tvShow.Id.CompareTo(afterTvShowId.Value) > 0)
                .OrderBy(tvShow => tvShow.Id)
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
                tvShowsProcessed++;
                afterTvShowId = tvShow.Id;
            }
        }

        return new ContentSearchTitleCatalogBackfillResult(moviesProcessed, tvShowsProcessed);
    }
}
