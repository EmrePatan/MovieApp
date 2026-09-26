using MovieApp.Application.Common;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Domain;

public sealed class SearchHistoryTests
{
    [Fact]
    public void CreateSetsRequiredFields()
    {
        var userId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        var history = SearchHistory.Create(userId, "Batman", QueryNormalizer.Normalize("Batman"), utcNow);

        Assert.Equal(userId, history.UserId);
        Assert.Equal("Batman", history.Query);
        Assert.Equal("Batman", history.NormalizedQuery);
        Assert.Equal(utcNow, history.SearchedAt);
    }

    [Fact]
    public void UpdateSearchedAtUpdatesTimestamp()
    {
        var history = SearchHistory.Create(Guid.NewGuid(), "Batman", "batman", DateTime.UtcNow.AddHours(-1));
        var updatedAt = DateTime.UtcNow;

        history.UpdateSearchedAt(updatedAt);

        Assert.Equal(updatedAt, history.SearchedAt);
    }
}
