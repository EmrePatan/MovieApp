using MovieApp.Application.Caching;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;

namespace MovieApp.UnitTests.Caching;

public sealed class RecommendationCacheKeysTests
{
    [Fact]
    public void SimilarMovieKeyIncludesAlgorithmVersion()
    {
        var movieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var key = RecommendationCacheKeys.SimilarMovie(movieId, 1, 20);

        Assert.Contains(movieId.ToString(), key);
        Assert.Contains(RecommendationAlgorithmVersion.Similar, key);
    }

    [Fact]
    public void UserKeyIncludesUserTypeAndPagination()
    {
        var userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var key = RecommendationCacheKeys.User(userId, RecommendationContentType.All, 2, 50);

        Assert.Contains(userId.ToString(), key);
        Assert.Contains("All", key);
        Assert.Contains("2", key);
        Assert.Contains("50", key);
    }

    [Fact]
    public void HomeKeyIncludesUserAndAlgorithmVersion()
    {
        var userId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var key = RecommendationCacheKeys.Home(userId);

        Assert.Contains(userId.ToString(), key);
        Assert.Contains(RecommendationAlgorithmVersion.Personalized, key);
    }
}
