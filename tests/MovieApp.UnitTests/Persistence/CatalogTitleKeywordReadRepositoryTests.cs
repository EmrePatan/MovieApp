using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

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

        var repository = CreateRepository(context);
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

        var repository = CreateRepository(context);
        var keywords = await repository.GetLocalizedKeywordsForMovieAsync(movieId, "en-US", maxCount: 3);

        Assert.Equal(3, keywords.Count);
    }

    [Fact]
    public async Task GetLocalizedKeywordsForMovieOmitsExcludedKeywords()
    {
        await using var context = CreateContext();
        var movieId = Guid.NewGuid();
        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Test Movie",
            TmdbId = 99,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        var usable = CreateKeyword("zombie apocalypse", displayable: true, displayRank: 200);
        usable.ClassificationStatus = KeywordClassificationStatus.Approved;
        var excluded = CreateKeyword("spam plot sentence", displayable: true, displayRank: 300);
        excluded.ClassificationStatus = KeywordClassificationStatus.Excluded;

        context.Keywords.AddRange(usable, excluded);
        context.MovieKeywords.AddRange(
            new MovieKeyword { MovieId = movieId, KeywordId = usable.Id },
            new MovieKeyword { MovieId = movieId, KeywordId = excluded.Id });
        await context.SaveChangesAsync();

        var repository = CreateRepository(context);
        var keywords = await repository.GetLocalizedKeywordsForMovieAsync(movieId, "en-US", maxCount: 30);

        Assert.Single(keywords);
        Assert.Equal(usable.Id, keywords[0].Id);
    }

    [Fact]
    public async Task GetLocalizedKeywordsForMovie_FillsWithSupplementalThemesWhenDisplayableCountIsLow()
    {
        await using var context = CreateContext();
        var movieId = Guid.NewGuid();
        context.Movies.Add(new Movie
        {
            Id = movieId,
            Title = "Test Movie",
            TmdbId = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        var displayable = CreateKeyword("zombie apocalypse", displayable: true, displayRank: 200, documentFrequency: 40);
        var supplemental = CreateKeyword("cult", displayable: false, displayRank: 0, documentFrequency: 8);
        var blocked = CreateKeyword("photograph", displayable: false, displayRank: 0, documentFrequency: 5);

        context.Keywords.AddRange(displayable, supplemental, blocked);
        context.MovieKeywords.AddRange(
            new MovieKeyword { MovieId = movieId, KeywordId = displayable.Id },
            new MovieKeyword { MovieId = movieId, KeywordId = supplemental.Id },
            new MovieKeyword { MovieId = movieId, KeywordId = blocked.Id });
        await context.SaveChangesAsync();

        var repository = CreateRepository(context);
        var keywords = await repository.GetLocalizedKeywordsForMovieAsync(movieId, "en-US", maxCount: 30);

        Assert.Equal(2, keywords.Count);
        Assert.Equal(displayable.Id, keywords[0].Id);
        Assert.Equal(supplemental.Id, keywords[1].Id);
        Assert.DoesNotContain(keywords, keyword => keyword.Name == "photograph");
    }

    private static CatalogTitleKeywordReadRepository CreateRepository(ApplicationDbContext context) =>
        new(context, Options.Create(new KeywordDisplayProfileOptions()));

    private static Keyword CreateKeyword(
        string name,
        bool displayable,
        int displayRank,
        int documentFrequency = 10)
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
                DocumentFrequency = documentFrequency,
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
