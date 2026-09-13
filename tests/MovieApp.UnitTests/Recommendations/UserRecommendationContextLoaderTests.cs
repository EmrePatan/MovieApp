using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Recommendations;

public sealed class UserRecommendationContextLoaderTests
{
    [Fact]
    public void TitleMatchesQueryUsesCaseInsensitiveContainsSemantics()
    {
        Assert.True(UserRecommendationContextLoader.TitleMatchesQuery("Inception", "cep"));
        Assert.True(UserRecommendationContextLoader.TitleMatchesQuery("INCEPTION", "inception"));
        Assert.False(UserRecommendationContextLoader.TitleMatchesQuery("Inception", "matrix"));
    }
}
