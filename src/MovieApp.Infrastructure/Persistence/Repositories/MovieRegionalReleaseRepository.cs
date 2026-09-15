using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class MovieRegionalReleaseRepository(ApplicationDbContext dbContext) : IMovieRegionalReleaseRepository
{
    public async Task<MovieRegionalRelease?> GetByMovieIdAndRegionAsync(
        Guid movieId,
        string region,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.MovieRegionalReleases
            .AsNoTracking()
            .FirstOrDefaultAsync(
                regionalRelease => regionalRelease.MovieId == movieId && regionalRelease.Region == region,
                cancellationToken);
    }

    public async Task<MovieRegionalRelease> UpsertAsync(
        MovieRegionalRelease regionalRelease,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.MovieRegionalReleases
            .FirstOrDefaultAsync(
                entry => entry.MovieId == regionalRelease.MovieId && entry.Region == regionalRelease.Region,
                cancellationToken);

        if (existing is null)
        {
            dbContext.MovieRegionalReleases.Add(regionalRelease);
        }
        else
        {
            existing.EffectiveReleaseDate = regionalRelease.EffectiveReleaseDate;
            existing.EffectiveReleaseType = regionalRelease.EffectiveReleaseType;
            existing.Certification = regionalRelease.Certification;
            existing.IsFallbackGlobal = regionalRelease.IsFallbackGlobal;
            existing.SyncedAtUtc = regionalRelease.SyncedAtUtc;
            regionalRelease = existing;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return regionalRelease;
    }
}
