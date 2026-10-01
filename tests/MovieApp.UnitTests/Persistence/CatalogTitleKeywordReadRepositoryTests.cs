using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MovieApp.UnitTests.Persistence;

public sealed class CatalogTitleKeywordReadRepositoryTests
{
    [Fact]
    public async Task GetLocalizedKeywordsForMovie_ReturnsOnlyDisplayableOrderedByDisplayRank()
    {
        await using var context = CreateContext();
        var movieId = Guid.NewGuid();
        var movie = new Movie
        {
            Id = movieId,
            Title = "Test Movie",
            TmdbId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var lowRankKeyword = CreateKeyword("elevator", displayable: true, displayRank: 10);
        var highRankKeyword = CreateKeyword("zombie apocalypse", displayable: true, displayRank: 200);
        var hiddenKeyword = CreateKeyword("photograph", displayable: false, displayRank: 150);

        context.Movies.Add(movie);
        context.Keywords.AddRange(lowRankKeyword, highRankKeyword, hiddenKeyword);
        context.MovieKeywords.AddRange(
            new MovieKeyword { MovieId = movieId, KeywordId = lowRankKeyword.Id },
            new MovieKeyword { MovieId = movieId, KeywordId = highRankKeyword.Id },
            new MovieKeyword { MovieId = movieId, KeywordId = hiddenKeyword.Id });
        await context.SaveChangesAsync();

        var repository = new CatalogTitleKeywordReadRepository(context);
        var keywords = await repository.GetLocalizedKeywordsForMovieAsync(
            movieId,
            "en-US",
            maxCount: 30);

        Assert.Equal(2, keywords.Count);
        Assert.Equal(highRankKeyword.Id, keywords[0].Id);
        Assert.Equal("zombie apocalypse", keywords[0].Name);
        Assert.Equal(lowRankKeyword.Id, keywords[1].Id);
    }

    [Fact]
    public async Task GetLocalizedKeywordsForMovie_RespectsMaxCount()
    {
        await using var context = CreateContext();
        var movieId = Guid.NewGuid();
        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Test Movie",
            TmdbId = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        for (var index = 0; index < 5; index++)
        {
            var keyword = CreateKeyword($"topic-{index}", displayable: true, displayRank: 100 - index);
            context.Keywords.Add(keyword);
            context.MovieKeywords.Add(new MovieKeyword { MovieId = movieId, KeywordId = keyword.Id });
        }

        await context.SaveChangesAsync();

        var repository = new CatalogTitleKeywordReadRepository(context);
        var keywords = await repository.GetLocalizedKeywordsForMovieAsync(movieId, "en-US", maxCount: 3);

        Assert.Equal(3, keywords.Count);
    }

    private static Keyword CreateKeyword(string name, bool displayable, int displayRank)
    {
        var keyword = new Keyword
        {
            Id = Guid.NewGuid(),
            Name = name,
            CanonicalName = name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DisplayProfile = new KeywordDisplayProfile
            {
                DocumentFrequency = 10,
                MovieTitleCount = 5,
                TvTitleCount = 1,
                Displayable = displayable,
                DisplayRank = displayRank,
                UpdatedAtUtc = DateTime.UtcNow,
            },
        };

        keyword.DisplayProfile!.KeywordId = keyword.Id;
        return keyword;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"catalog-title-keywords-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
