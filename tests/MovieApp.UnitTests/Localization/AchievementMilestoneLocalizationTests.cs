using MovieApp.Application.Models.Identity;
using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Identity;
using MovieApp.Application.Services.Insights;
using MovieApp.Application.Services.Localization;
using MovieApp.Api.Mapping;

namespace MovieApp.UnitTests.Localization;

public sealed class AchievementMilestoneLocalizationTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("en")]
    [InlineData(null)]
    public void GetTitle_ReturnsEnglishForNonTurkishLocale(string? contentLocale)
    {
        var title = AchievementMilestoneLocalization.GetTitle(
            "first-movie",
            contentLocale ?? string.Empty,
            "fallback");

        Assert.Equal("First movie watched", title);
    }

    [Theory]
    [InlineData("es-ES")]
    [InlineData("es")]
    [InlineData("es-ES,en-US;q=0.8")]
    public void GetTitle_ReturnsSpanishForSpanishLocale(string contentLocale)
    {
        var title = AchievementMilestoneLocalization.GetTitle(
            "movies-10",
            contentLocale,
            "fallback");

        Assert.Equal("10 películas vistas", title);
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("tr")]
    [InlineData("tr-TR,en-US;q=0.8")]
    public void GetTitle_ReturnsTurkishForTurkishLocale(string contentLocale)
    {
        var title = AchievementMilestoneLocalization.GetTitle(
            "movies-10",
            contentLocale,
            "fallback");

        Assert.Equal("10 film izlendi", title);
    }

    [Fact]
    public void GetDescription_ReturnsTurkishProfileCopy()
    {
        var description = AchievementMilestoneLocalization.GetDescription(
            "movies-50",
            ContentLocaleResolver.TurkishTurkey,
            "fallback");

        Assert.Equal("Ciddi bir izleme geçmişi oluşturuyorsun.", description);
    }

    [Fact]
    public void GetTitle_UsesFallbackForUnknownMilestoneId()
    {
        var title = AchievementMilestoneLocalization.GetTitle(
            "unknown-milestone",
            ContentLocaleResolver.TurkishTurkey,
            "Custom fallback");

        Assert.Equal("Custom fallback", title);
    }

    [Fact]
    public void InsightsV3Mapper_LocalizesAchievementTitlesWithoutChangingIds()
    {
        var raw = new InsightsAnalyticsRawData(
            DateTime.UtcNow,
            10,
            0,
            0,
            0,
            [],
            [],
            [],
            0,
            0,
            0,
            0,
            [],
            [],
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        var achievements = InsightsMilestonesBuilder.Build(raw);
        var insights = CreateMinimalV3Result(achievements);

        var english = InsightsContractMapper.ToInsightsV3Response(
            insights,
            ContentLocaleResolver.EnglishUnitedStates);
        var turkish = InsightsContractMapper.ToInsightsV3Response(
            insights,
            ContentLocaleResolver.TurkishTurkey);

        var englishMilestone = english.Achievements.Single(item => item.Id == "movies-10");
        var turkishMilestone = turkish.Achievements.Single(item => item.Id == "movies-10");

        Assert.Equal("10 movies watched", englishMilestone.Title);
        Assert.Equal("10 film izlendi", turkishMilestone.Title);
        Assert.Equal(englishMilestone.Id, turkishMilestone.Id);
        Assert.Equal(englishMilestone.Category, turkishMilestone.Category);
        Assert.Equal(englishMilestone.TargetValue, turkishMilestone.TargetValue);
    }

    [Fact]
    public void UserStatisticsMapper_LocalizesMilestoneTitleAndDescription()
    {
        var statistics = ProfileStatisticsBuilder.Build(
            new ProfileStatisticsRawData(
                0,
                0,
                0,
                0,
                10,
                0,
                0,
                0,
                10,
                0,
                0,
                0,
                [],
                [],
                [],
                [],
                null,
                null),
            new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc));

        var english = UserProfileContractMapper.ToUserStatisticsResponse(
            statistics,
            ContentLocaleResolver.EnglishUnitedStates);
        var turkish = UserProfileContractMapper.ToUserStatisticsResponse(
            statistics,
            ContentLocaleResolver.TurkishTurkey);

        var englishMilestone = english.Milestones.Single(item => item.Id == "ratings-10");
        var turkishMilestone = turkish.Milestones.Single(item => item.Id == "ratings-10");

        Assert.Equal("10 ratings", englishMilestone.Title);
        Assert.Equal("You are shaping your taste profile.", englishMilestone.Description);
        Assert.Equal("10 puan", turkishMilestone.Title);
        Assert.Equal("Zevk profilini şekillendiriyorsun.", turkishMilestone.Description);
    }

    private static InsightsV3Result CreateMinimalV3Result(IReadOnlyList<InsightsMilestoneResult> achievements)
    {
        var generatedAt = DateTime.UtcNow;
        var emptyMix = new InsightsV3WatchingMixResult(0, 0, 0m, 0m);
        return new InsightsV3Result(
            new InsightsV3MetaResult(generatedAt, generatedAt, "UTC", generatedAt.Year),
            new InsightsV3MovieDnaResult(string.Empty, [], [], [], emptyMix),
            new InsightsV3YourYearResult([], 0, null, null),
            new InsightsV3TasteSectionResult([], null),
            new InsightsV3TimeInStoriesResult(0, 0, 0, 0, 0m),
            new InsightsV3RatingsSectionResult(0, null, [], null, null),
            new InsightsV3EraSectionResult([], null, 0, null),
            new InsightsV3RecordsSectionResult(null, null, null, null),
            achievements);
    }
}
