using MovieApp.Application.Services.Keywords;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.UnitTests.Keywords;

public sealed class TmdbKeywordsMapperTests
{
    [Fact]
    public void ToProviderKeywordsMapsMovieEnvelope()
    {
        var keywords = TmdbKeywordsMapper.ToProviderKeywords(
        [
            new TmdbKeywordItemJson { Id = 42, Name = " time travel " },
            new TmdbKeywordItemJson { Id = 0, Name = "invalid" },
            new TmdbKeywordItemJson { Id = 99, Name = "   " },
        ]);

        Assert.Equal([42], keywords.Select(keyword => keyword.TmdbKeywordId).ToArray());
        Assert.Equal("time travel", keywords[0].Name);
    }

    [Fact]
    public void NormalizeRejectsDuplicateTmdbIds()
    {
        var normalized = KeywordNormalization.Normalize(
        [
            new(42, "alpha"),
            new(42, "beta"),
        ]);

        Assert.Single(normalized);
        Assert.Equal("beta", normalized[0].Name);
    }
}
