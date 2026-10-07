using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Library;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class LibraryShowCompletionEquivalenceTests
{
    [Fact]
    public async Task LibraryCategoriesSearchAndCursorsMatchCompletionRules()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        var userId = await SeedUserAsync(context, $"library-equiv-{Guid.NewGuid():N}");
        var utc = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        var later = utc.AddDays(1);
        var earlier = utc.AddDays(-1);
        var juneSecond = new DateTime(2024, 6, 2, 8, 0, 0, DateTimeKind.Utc);
        var juneFirst = new DateTime(2024, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var mayFirst = new DateTime(2024, 5, 1, 8, 0, 0, DateTimeKind.Utc);
        var aprilFirst = new DateTime(2024, 4, 1, 8, 0, 0, DateTimeKind.Utc);
        var yuzukAt = new DateTime(2023, 12, 1, 8, 0, 0, DateTimeKind.Utc);

        var starLaterId = await AddMovieAsync(context, "Star Later", utc);
        var tieMovieId = await AddMovieAsync(context, "Tie Movie", utc);
        var earlierMovieId = await AddMovieAsync(context, "Earlier Movie", utc);
        var yuzukId = await AddMovieAsync(context, "Yüzüklerin Efendisi", utc);
        context.WatchedMovies.AddRange(
            WatchedMovie.Create(userId, starLaterId, later),
            WatchedMovie.Create(userId, tieMovieId, utc),
            WatchedMovie.Create(userId, earlierMovieId, earlier),
            WatchedMovie.Create(userId, yuzukId, yuzukAt));

        var (starPartialId, starPartialNextId) = await AddShowAsync(
            context,
            userId,
            "Star Partial",
            TvShowStatus.ReturningSeries,
            regularEpisodeCount: 2,
            watchedEpisodes: 1,
            lastWatchedAt: juneSecond,
            emptySeasonEpisodeCount: null);
        var olderPartialId = (await AddShowAsync(
            context,
            userId,
            "Older Partial",
            TvShowStatus.ReturningSeries,
            regularEpisodeCount: 2,
            watchedEpisodes: 1,
            lastWatchedAt: mayFirst,
            emptySeasonEpisodeCount: null)).ShowId;
        var caughtUpId = (await AddShowAsync(
            context,
            userId,
            "Caught Up",
            TvShowStatus.ReturningSeries,
            regularEpisodeCount: 1,
            watchedEpisodes: 1,
            lastWatchedAt: juneFirst,
            emptySeasonEpisodeCount: null)).ShowId;
        var uningestedId = (await AddShowAsync(
            context,
            userId,
            "Uningested Season",
            TvShowStatus.Ended,
            regularEpisodeCount: 1,
            watchedEpisodes: 1,
            lastWatchedAt: aprilFirst,
            emptySeasonEpisodeCount: 4)).ShowId;
        var completedId = (await AddShowAsync(
            context,
            userId,
            "Completed Show",
            TvShowStatus.Ended,
            regularEpisodeCount: 1,
            watchedEpisodes: 1,
            lastWatchedAt: utc,
            emptySeasonEpisodeCount: null)).ShowId;
        var negativeTotalId = (await AddShowAsync(
            context,
            userId,
            "Negative Placeholder",
            TvShowStatus.Ended,
            regularEpisodeCount: 1,
            watchedEpisodes: 1,
            lastWatchedAt: new DateTime(2024, 3, 1, 8, 0, 0, DateTimeKind.Utc),
            emptySeasonEpisodeCount: -3)).ShowId;
        await AddSpecialsOnlyShowAsync(context, userId, utc);

        var likedId = await AddMovieAsync(context, "Star Liked", utc);
        context.Favorites.Add(Favorite.CreateForMovie(userId, likedId, utc));
        var savedId = await AddMovieAsync(context, "Star Saved", utc);
        var watchlist = Watchlist.Create(userId, "Main", utc);
        watchlist.Items.Add(WatchlistItem.CreateForMovie(watchlist.Id, savedId, utc));
        context.Watchlists.Add(watchlist);
        await context.SaveChangesAsync();

        var repository = new LibraryRepository(context);
        var page = new LibraryPageRequest(1, 24, 25, null, LibraryCountMode.Required);

        var (watching, watchingTotal) = await repository.GetWatchingAsync(userId, SearchContentType.All, page);
        Assert.Equal(5, watchingTotal);
        Assert.Equal(
            [starPartialId, olderPartialId, uningestedId, caughtUpId, negativeTotalId],
            watching.Select(item => item.Id).ToArray());
        Assert.Equal(50m, watching[0].ProgressPercentage);
        if (watching[0].NextEpisode is not { } nextEpisode)
        {
            throw new InvalidOperationException("Expected the next unwatched episode.");
        }

        Assert.Equal(starPartialNextId, nextEpisode.EpisodeId);
        Assert.Equal(2, nextEpisode.EpisodeNumber);
        Assert.Equal(20m, watching.Single(item => item.Id == uningestedId).ProgressPercentage);
        Assert.Equal(-50m, watching.Single(item => item.Id == negativeTotalId).ProgressPercentage);
        Assert.Equal(100m, watching.Single(item => item.Id == caughtUpId).ProgressPercentage);
        Assert.Null(watching.Single(item => item.Id == caughtUpId).NextEpisode);
        Assert.DoesNotContain(watching, item => item.Id == completedId);

        var (watchedAll, watchedAllTotal) = await repository.GetWatchedAsync(userId, SearchContentType.All, page);
        Assert.Equal(5, watchedAllTotal);
        Assert.Equal(
            [starLaterId, tieMovieId, completedId, earlierMovieId, yuzukId],
            watchedAll.Select(item => item.Id).ToArray());
        Assert.Equal(100m, watchedAll.Single(item => item.Id == completedId).ProgressPercentage);
        Assert.All(watchedAll, item => Assert.Equal("watched", item.CollectionStatus));

        var (watchedTv, watchedTvTotal) = await repository.GetWatchedAsync(userId, SearchContentType.Tv, page);
        Assert.Equal(1, watchedTvTotal);
        Assert.Equal(completedId, Assert.Single(watchedTv).Id);

        var (watchedMovies, watchedMovieTotal) = await repository.GetWatchedAsync(userId, SearchContentType.Movie, page);
        Assert.Equal(4, watchedMovieTotal);
        Assert.Equal([starLaterId, tieMovieId, earlierMovieId, yuzukId], watchedMovies.Select(item => item.Id).ToArray());

        var service = CreateService(context, userId);
        var firstPage = await service.GetLibraryAsync(
            new LibraryCriteria(LibraryCategory.Watched, SearchContentType.All, 1, 2),
            ContentLocaleResolver.EnglishUnitedStates);
        Assert.Equal(5, firstPage.TotalCount);
        Assert.Equal(3, firstPage.TotalPages);
        Assert.True(firstPage.HasNextPage);
        Assert.Equal([starLaterId, tieMovieId], firstPage.Items.Select(item => item.Id).ToArray());

        var secondPage = await service.GetLibraryAsync(
            new LibraryCriteria(LibraryCategory.Watched, SearchContentType.All, 1, 2, firstPage.NextCursor),
            ContentLocaleResolver.EnglishUnitedStates);
        Assert.Equal(5, secondPage.TotalCount);
        Assert.Equal([completedId, earlierMovieId], secondPage.Items.Select(item => item.Id).ToArray());

        var watchingPage = await service.GetLibraryAsync(
            new LibraryCriteria(LibraryCategory.Watching, SearchContentType.Tv, 1, 2),
            ContentLocaleResolver.EnglishUnitedStates);
        Assert.Equal(5, watchingPage.TotalCount);
        Assert.Equal([starPartialId, olderPartialId], watchingPage.Items.Select(item => item.Id).ToArray());
        var watchingNext = await service.GetLibraryAsync(
            new LibraryCriteria(LibraryCategory.Watching, SearchContentType.Tv, 1, 2, watchingPage.NextCursor),
            ContentLocaleResolver.EnglishUnitedStates);
        Assert.Equal([uningestedId, caughtUpId], watchingNext.Items.Select(item => item.Id).ToArray());

        var search = await service.SearchLibraryAsync(
            new LibrarySearchCriteria("star", SearchContentType.All, 1, 24),
            ContentLocaleResolver.EnglishUnitedStates);
        Assert.Equal(
            ["Star Later", "Star Liked", "Star Partial", "Star Saved"],
            search.Items.Select(item => item.Title).ToArray());
        Assert.Equal("watched", search.Items[0].CollectionStatus);
        Assert.Equal("liked", search.Items[1].CollectionStatus);
        Assert.Equal("watching", search.Items[2].CollectionStatus);
        Assert.Equal("watchlist", search.Items[3].CollectionStatus);
        Assert.Equal(4, search.TotalCount);

        var turkish = await service.SearchLibraryAsync(
            new LibrarySearchCriteria("yüzük", SearchContentType.All, 1, 24),
            ContentLocaleResolver.EnglishUnitedStates);
        Assert.Equal(yuzukId, Assert.Single(turkish.Items).Id);
    }

    private static LibraryService CreateService(ApplicationDbContext context, Guid userId) =>
        new(
            new LibraryRepository(context),
            new FixedCurrentUser(userId),
            new ContentLocalizedPosterRepository(context),
            CatalogRepositoryTestFactory.CreateMovieRepository(context),
            CatalogRepositoryTestFactory.CreateTvShowRepository(context),
            NullLogger<LibraryService>.Instance);

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
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> AddMovieAsync(ApplicationDbContext context, string title, DateTime utcNow)
    {
        var movieId = Guid.NewGuid();
        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = title,
            VoteAverage = 7,
            VoteCount = 10,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        await context.SaveChangesAsync();
        return movieId;
    }

    private static async Task<(Guid ShowId, Guid? NextEpisodeId)> AddShowAsync(
        ApplicationDbContext context,
        Guid userId,
        string title,
        TvShowStatus status,
        int regularEpisodeCount,
        int watchedEpisodes,
        DateTime lastWatchedAt,
        int? emptySeasonEpisodeCount)
    {
        var utcNow = lastWatchedAt;
        var showId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = showId,
            Title = title,
            Status = status,
            VoteAverage = 8,
            VoteCount = 20,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = showId,
            SeasonNumber = 1,
            EpisodeCount = regularEpisodeCount,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });

        Guid? nextEpisodeId = null;
        for (var number = 1; number <= regularEpisodeCount; number++)
        {
            var episodeId = Guid.NewGuid();
            context.Episodes.Add(new Episode
            {
                Id = episodeId,
                SeasonId = seasonId,
                EpisodeNumber = number,
                Name = $"Episode {number}",
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
            if (number <= watchedEpisodes)
            {
                var watchedAt = number == watchedEpisodes
                    ? lastWatchedAt
                    : lastWatchedAt.AddDays(-number);
                context.WatchedEpisodes.Add(WatchedEpisode.Create(userId, episodeId, watchedAt));
            }
            else if (nextEpisodeId is null)
            {
                nextEpisodeId = episodeId;
            }
        }

        if (emptySeasonEpisodeCount is not null)
        {
            context.Seasons.Add(new Season
            {
                Id = Guid.NewGuid(),
                TvShowId = showId,
                SeasonNumber = 2,
                EpisodeCount = emptySeasonEpisodeCount,
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
            });
        }

        await context.SaveChangesAsync();
        return (showId, nextEpisodeId);
    }

    private static async Task AddSpecialsOnlyShowAsync(
        ApplicationDbContext context,
        Guid userId,
        DateTime utcNow)
    {
        var showId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        context.TvShows.Add(new TvShow
        {
            Id = showId,
            Title = "Specials Only",
            Status = TvShowStatus.Ended,
            VoteAverage = 6,
            VoteCount = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.Seasons.Add(new Season
        {
            Id = seasonId,
            TvShowId = showId,
            SeasonNumber = 0,
            EpisodeCount = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.Episodes.Add(new Episode
        {
            Id = episodeId,
            SeasonId = seasonId,
            EpisodeNumber = 1,
            Name = "Special",
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        });
        context.WatchedEpisodes.Add(WatchedEpisode.Create(userId, episodeId, utcNow));
        await context.SaveChangesAsync();
    }

    private sealed class FixedCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }
}
