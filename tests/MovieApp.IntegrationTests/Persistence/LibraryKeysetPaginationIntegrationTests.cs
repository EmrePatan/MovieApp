using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Library;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class LibraryKeysetPaginationIntegrationTests
{
    [Fact]
    public async Task LikedKeysetPagesAreContiguousWithoutDuplicates()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = await SeedUserAsync(context, $"library-keyset-{Guid.NewGuid():N}");
        var utcNow = DateTime.UtcNow;

        for (var index = 0; index < 25; index++)
        {
            var movieId = Guid.NewGuid();
            context.Movies.Add(new Movie
            {
                Id = movieId,
                Title = $"Library Liked {index:D2}",
                VoteAverage = 7,
                VoteCount = 100,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
            context.Favorites.Add(Favorite.CreateForMovie(
                userId,
                movieId,
                utcNow.AddMinutes(-index)));
        }

        await context.SaveChangesAsync();

        var service = CreateService(context, userId);
        const int pageSize = 10;
        var criteria = new LibraryCriteria(LibraryCategory.Liked, SearchContentType.Movie, 1, pageSize);

        var page1 = await service.GetLibraryAsync(criteria);
        Assert.Equal(10, page1.Items.Count);
        Assert.NotNull(page1.NextCursor);
        Assert.True(page1.HasNextPage);
        Assert.Equal(25, page1.TotalCount);

        var page2 = await service.GetLibraryAsync(criteria with { Cursor = page1.NextCursor, Page = 1 });
        Assert.Equal(10, page2.Items.Count);
        Assert.Equal(2, page2.Page);
        Assert.NotNull(page2.NextCursor);
        Assert.Equal(25, page2.TotalCount);

        var page3 = await service.GetLibraryAsync(criteria with { Cursor = page2.NextCursor, Page = 1 });
        Assert.Equal(5, page3.Items.Count);
        Assert.Null(page3.NextCursor);
        Assert.False(page3.HasNextPage);
        Assert.Equal(25, page3.TotalCount);

        var allIds = page1.Items.Concat(page2.Items).Concat(page3.Items).Select(item => item.Id).ToList();
        Assert.Equal(25, allIds.Distinct().Count());
    }

    [Fact]
    public async Task LikedCursorContinuationSkipsCountQuery()
    {
        await using var context = CreateInstrumentedContext(out var interceptor);
        var userId = await SeedUserAsync(context, $"library-count-{Guid.NewGuid():N}");
        var utcNow = DateTime.UtcNow;

        for (var index = 0; index < 25; index++)
        {
            var movieId = Guid.NewGuid();
            context.Movies.Add(new Movie
            {
                Id = movieId,
                Title = $"Library Count {index}",
                VoteAverage = 7,
                VoteCount = 50,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
            context.Favorites.Add(Favorite.CreateForMovie(userId, movieId, utcNow.AddMinutes(-index)));
        }

        await context.SaveChangesAsync();

        var service = CreateService(context, userId);
        var criteria = new LibraryCriteria(LibraryCategory.Liked, SearchContentType.Movie, 1, 10);

        interceptor.Reset();
        var page1 = await service.GetLibraryAsync(criteria);
        Assert.Equal(1, interceptor.CountQueryCount);

        interceptor.Reset();
        var page2 = await service.GetLibraryAsync(criteria with { Cursor = page1.NextCursor, Page = 1 });
        Assert.Equal(0, interceptor.CountQueryCount);
        Assert.Equal(page1.TotalCount, page2.TotalCount);
        Assert.Equal(10, page2.Items.Count);
    }

    [Fact]
    public async Task LegacyOffsetPageStillExecutesCount()
    {
        await using var context = CreateInstrumentedContext(out var interceptor);
        var userId = await SeedUserAsync(context, $"library-offset-{Guid.NewGuid():N}");
        var utcNow = DateTime.UtcNow;

        for (var index = 0; index < 25; index++)
        {
            var movieId = Guid.NewGuid();
            context.Movies.Add(new Movie
            {
                Id = movieId,
                Title = $"Library Offset {index}",
                VoteAverage = 7,
                VoteCount = 50,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });
            context.Favorites.Add(Favorite.CreateForMovie(userId, movieId, utcNow.AddMinutes(-index)));
        }

        await context.SaveChangesAsync();

        var service = CreateService(context, userId);
        interceptor.Reset();
        var page2 = await service.GetLibraryAsync(
            new LibraryCriteria(LibraryCategory.Liked, SearchContentType.Movie, 2, 10));

        Assert.Equal(1, interceptor.CountQueryCount);
        Assert.Equal(10, page2.Items.Count);
        Assert.Equal(25, page2.TotalCount);
    }

    [Fact]
    public async Task MalformedCursorIsRejected()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = await SeedUserAsync(context, $"library-bad-cursor-{Guid.NewGuid():N}");
        var service = CreateService(context, userId);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.GetLibraryAsync(new LibraryCriteria(
                LibraryCategory.Liked,
                SearchContentType.Movie,
                1,
                10,
                "not-a-valid-cursor")));
    }

    [Fact]
    public async Task LikedCursorContinuationSqlDoesNotUseOffset()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var repository = new LibraryRepository(context);
        var userId = Guid.NewGuid();
        var cursor = MovieApp.Application.Library.LibraryKeysetCursor.Encode(
            MovieApp.Application.Library.LibraryKeysetCursor.CreateDateAnchor(
                userId,
                new LibraryCriteria(LibraryCategory.Liked, SearchContentType.Movie, 1, 24),
                1,
                100,
                DateTime.UtcNow,
                null,
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")));

        var decoded = MovieApp.Application.Library.LibraryKeysetCursor.TryDecode(
            cursor,
            userId,
            new LibraryCriteria(LibraryCategory.Liked, SearchContentType.Movie, 1, 24),
            out var parsed,
            out _);
        Assert.True(decoded);
        Assert.NotNull(parsed);

        var request = new LibraryPageRequest(2, 24, 25, parsed, false);
        var query = context.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.UserId == userId)
            .Where(favorite => favorite.MovieId != null)
            .OrderByDescending(favorite => favorite.CreatedAt)
            .ThenBy(favorite => favorite.MovieId ?? favorite.TvShowId ?? favorite.Id)
            .Where(favorite =>
                favorite.CreatedAt < parsed!.GetSortInstant()
                || (favorite.CreatedAt == parsed.GetSortInstant()
                    && (favorite.MovieId ?? favorite.TvShowId ?? favorite.Id).CompareTo(parsed.PrimaryId) > 0))
            .Take(request.FetchLimit);

        var sql = query.ToQueryString();
        Assert.DoesNotContain("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static LibraryService CreateService(ApplicationDbContext context, Guid userId) =>
        new(new LibraryRepository(context), new FixedCurrentUser(userId), NullLogger<LibraryService>.Instance);

    private static async Task<Guid> SeedUserAsync(ApplicationDbContext context, string userName)
    {
        var utcNow = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = userId,
            Email = $"{userName}@example.com",
            NormalizedEmail = $"{userName}@example.com".ToUpperInvariant(),
            UserName = userName,
            DisplayName = userName,
            PasswordHash = "hash",
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

        await context.SaveChangesAsync();
        return userId;
    }

    private static ApplicationDbContext CreateInstrumentedContext(out LibraryCountQueryInterceptor interceptor)
    {
        interceptor = new LibraryCountQueryInterceptor();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                IntegrationTestDatabase.GetConnectionString(),
                npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3))
            .AddInterceptors(interceptor)
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class LibraryCountQueryInterceptor : DbCommandInterceptor
    {
        public int CountQueryCount { get; private set; }

        public void Reset() => CountQueryCount = 0;

        public override DbDataReader ReaderExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result)
        {
            if (IsCountQuery(command.CommandText))
            {
                CountQueryCount++;
            }

            return base.ReaderExecuted(command, eventData, result);
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (IsCountQuery(command.CommandText))
            {
                CountQueryCount++;
            }

            return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }

        private static bool IsCountQuery(string commandText) =>
            commandText.Contains("count(", StringComparison.OrdinalIgnoreCase);
    }
}
