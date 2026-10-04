using MovieApp.Application.Models.Providers;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence.Keywords;

namespace MovieApp.UnitTests.Keywords;

public sealed class KeywordGraphTmdbKeywordDualWriteClassificationTests
{
    [Theory]
    [InlineData(KeywordClassificationStatus.Approved)]
    [InlineData(KeywordClassificationStatus.Excluded)]
    [InlineData(KeywordClassificationStatus.Auto)]
    public void ApplyMetadataAndNameUpdatesPreservesClassificationStatus(KeywordClassificationStatus status)
    {
        var keyword = new Keyword
        {
            Id = Guid.NewGuid(),
            TmdbKeywordId = 42,
            Name = "old name",
            CanonicalName = "old name",
            ClassificationStatus = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var keywordsByTmdbId = new Dictionary<int, Keyword> { [42] = keyword };
        var syncedAtUtc = DateTime.UtcNow.AddMinutes(1);

        KeywordGraphTmdbKeywordDualWrite.ApplyMetadataAndNameUpdates(
            keywordsByTmdbId,
            [new ProviderKeywordSummary(42, "renamed provider label")],
            syncedAtUtc);

        Assert.Equal(status, keyword.ClassificationStatus);
        Assert.Equal("renamed provider label", keyword.Name);
    }

    [Fact]
    public void PrepareNewKeywordEntitySetsAutoClassification()
    {
        var keyword = new Keyword { Id = Guid.NewGuid() };
        var syncedAtUtc = DateTime.UtcNow;

        KeywordGraphTmdbKeywordDualWrite.PrepareNewKeywordEntity(
            keyword,
            new ProviderKeywordSummary(7, "new keyword"),
            syncedAtUtc);

        Assert.Equal(KeywordClassificationStatus.Auto, keyword.ClassificationStatus);
    }
}
