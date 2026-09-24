using MovieApp.Domain.Ratings;

namespace MovieApp.UnitTests.Ratings;

public sealed class RatingStarMappingTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(6, 3)]
    [InlineData(7, 4)]
    [InlineData(8, 4)]
    [InlineData(9, 5)]
    [InlineData(10, 5)]
    public void PersistedTenPointScoreMapsToFiveStarBucketWithHalfStarsRoundingUp(
        int persistedScore,
        int starBucket)
    {
        Assert.Equal(starBucket, RatingStarMapping.ToStarBucket(persistedScore));
        Assert.Equal(starBucket, (persistedScore + 1) / 2);
    }

    [Fact]
    public void HalfStarBoundariesShareTheSameIntegerBucketAsTheFullStarAboveThem()
    {
        Assert.Equal(RatingStarMapping.ToStarBucket(3), RatingStarMapping.ToStarBucket(4));
        Assert.Equal(RatingStarMapping.ToStarBucket(1), RatingStarMapping.ToStarBucket(2));
        Assert.Equal(2, RatingStarMapping.ToStarBucket(3));
    }
}
