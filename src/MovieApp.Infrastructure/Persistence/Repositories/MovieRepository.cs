using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Common;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class MovieRepository(ApplicationDbContext dbContext) : IMovieRepository
{
    public async Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Movies
            .AsNoTracking()
            .Include(movie => movie.MovieGenres)
            .ThenInclude(movieGenre => movieGenre.Genre)
            .FirstOrDefaultAsync(movie => movie.Id == id, cancellationToken);
    }

    public async Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Movies
            .Include(movie => movie.MovieGenres)
            .ThenInclude(movieGenre => movieGenre.Genre)
            .FirstOrDefaultAsync(movie => movie.TmdbId == tmdbId, cancellationToken);
    }

    public async Task<Movie> UpsertFromProviderAsync(
        MovieProviderDetails details,
        CancellationToken cancellationToken = default)
    {
        Movie? movie = null;

        if (details.TmdbId.HasValue)
        {
            movie = await dbContext.Movies
                .Include(existingMovie => existingMovie.MovieGenres)
                .ThenInclude(movieGenre => movieGenre.Genre)
                .FirstOrDefaultAsync(
                    existingMovie => existingMovie.TmdbId == details.TmdbId,
                    cancellationToken);
        }

        var utcNow = DateTime.UtcNow;

        if (movie is null)
        {
            movie = new Movie
            {
                Id = Guid.NewGuid(),
                CreatedAt = utcNow
            };

            dbContext.Movies.Add(movie);
        }

        ApplyProviderDetails(movie, details, utcNow);

        await SyncGenresAsync(movie, details.Genres, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionExtensions.IsUniqueConstraintViolation(exception))
        {
            throw new MovieExternalIdPersistenceConflictException(
                details.TmdbId,
                details.ExternalId,
                exception);
        }

        return movie;
    }

    public async Task<IReadOnlyList<Movie>> UpsertFromProviderBatchAsync(
        IReadOnlyList<MovieProviderDetails> details,
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

        var existingMovies = tmdbIds.Count == 0
            ? []
            : await dbContext.Movies
                .Include(movie => movie.MovieGenres)
                .ThenInclude(movieGenre => movieGenre.Genre)
                .Where(movie => movie.TmdbId.HasValue && tmdbIds.Contains(movie.TmdbId.Value))
                .ToListAsync(cancellationToken);

        var moviesByTmdbId = existingMovies
            .Where(movie => movie.TmdbId.HasValue)
            .ToDictionary(movie => movie.TmdbId!.Value);

        var genresByName = await LoadGenresByNameAsync(
            details.SelectMany(detail => detail.Genres),
            cancellationToken);

        var utcNow = DateTime.UtcNow;
        var results = new List<Movie>(details.Count);

        foreach (var detail in details)
        {
            Movie movie;

            if (detail.TmdbId.HasValue && moviesByTmdbId.TryGetValue(detail.TmdbId.Value, out var existingMovie))
            {
                movie = existingMovie;
            }
            else
            {
                movie = new Movie
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = utcNow
                };

                dbContext.Movies.Add(movie);

                if (detail.TmdbId.HasValue)
                {
                    moviesByTmdbId[detail.TmdbId.Value] = movie;
                }
            }

            ApplyProviderDetails(movie, detail, utcNow);
            SyncGenresWithContext(movie, detail.Genres, genresByName, utcNow);
            results.Add(movie);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionExtensions.IsUniqueConstraintViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            throw new MovieExternalIdPersistenceConflictException(
                null,
                string.Empty,
                exception);
        }

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

        return await dbContext.Movies
            .AsNoTracking()
            .Where(movie => movie.TmdbId.HasValue && distinctIds.Contains(movie.TmdbId.Value))
            .Select(movie => new { movie.TmdbId, movie.Id })
            .ToDictionaryAsync(
                movie => movie.TmdbId!.Value,
                movie => movie.Id,
                cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
        IReadOnlyList<MovieProviderSummary> summaries,
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

            var movie = new Movie
            {
                Id = Guid.NewGuid(),
                TmdbId = summary.TmdbId,
                TvdbId = summary.TvdbId,
                ImdbId = ImdbIdNormalizer.Normalize(summary.ImdbId),
                Title = summary.Title,
                Overview = summary.Overview,
                ReleaseDate = summary.ReleaseDate,
                PosterPath = summary.PosterPath,
                VoteAverage = summary.VoteAverage,
                VoteCount = summary.VoteCount,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

            dbContext.Movies.Add(movie);
            mutableExistingIds[summary.TmdbId.Value] = movie.Id;
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
        Movie movie,
        IReadOnlyList<string> genreNames,
        CancellationToken cancellationToken)
    {
        var genresByName = await LoadGenresByNameAsync(genreNames, cancellationToken);
        SyncGenresWithContext(movie, genreNames, genresByName, DateTime.UtcNow);
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

    private static void ApplyProviderDetails(Movie movie, MovieProviderDetails details, DateTime utcNow)
    {
        movie.TmdbId = details.TmdbId;
        movie.TvdbId = details.TvdbId;
        movie.ImdbId = ImdbIdNormalizer.Normalize(details.ImdbId);
        movie.Title = details.Title;
        movie.OriginalTitle = details.OriginalTitle;
        movie.Overview = details.Overview;
        movie.ReleaseDate = details.ReleaseDate;
        movie.RuntimeMinutes = details.RuntimeMinutes;
        movie.PosterPath = details.PosterPath;
        movie.BackdropPath = details.BackdropPath;
        movie.OriginalLanguage = details.OriginalLanguage;
        movie.VoteAverage = details.VoteAverage;
        movie.VoteCount = details.VoteCount;
        movie.UpdatedAt = utcNow;
    }

    private void SyncGenresWithContext(
        Movie movie,
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

            var alreadyLinked = movie.MovieGenres.Any(movieGenre => movieGenre.GenreId == genre.Id);
            if (!alreadyLinked)
            {
                movie.MovieGenres.Add(new MovieGenre
                {
                    MovieId = movie.Id,
                    GenreId = genre.Id,
                    Genre = genre,
                    Movie = movie
                });
            }
        }

        var genresToRemove = movie.MovieGenres
            .Where(movieGenre => !linkedGenreIds.Contains(movieGenre.GenreId))
            .ToList();

        foreach (var movieGenre in genresToRemove)
        {
            movie.MovieGenres.Remove(movieGenre);
        }
    }
}
