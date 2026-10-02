using MovieApp.Application.Services.Discovery;

namespace MovieApp.UnitTests.Discovery;

public sealed class OnTvThisWeekQualityFilterTests
{
    [Theory]
    [InlineData(40, "/poster.jpg", true)]
    [InlineData(39, "/poster.jpg", false)]
    [InlineData(80, null, false)]
    [InlineData(80, " ", false)]
    public void IncludeRequiresMinimumVotesAndPoster(int voteCount, string? posterPath, bool expected)
    {
        Assert.Equal(expected, OnTvThisWeekQualityFilter.Include(voteCount, posterPath));
    }
}
