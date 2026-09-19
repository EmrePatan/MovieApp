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
    public void InsightsAnalyticsMapper_LocalizesAchievementTitlesWithoutChangingIds()
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

        var analytics = new InsightsAnalyticsResult(
            new InsightsActivityResult([], new InsightsActivitySummaryResult(0, null, null, 0, 0)),
            new InsightsTasteResult([]),
            new InsightsErasResult([], 0),
            new InsightsEstimatedTimeWatchedResult(0, 0, 0, 0, 0, 0, 0),
            new InsightsRatingsAnalyticsResult(0, null, [], null),
            InsightsMilestonesBuilder.Build(raw),
            DateTime.UtcNow);

        var english = InsightsContractMapper.ToInsightsAnalyticsResponse(
            analytics,
            ContentLocaleResolver.EnglishUnitedStates);
        var turkish = InsightsContractMapper.ToInsightsAnalyticsResponse(
            analytics,
            ContentLocaleResolver.TurkishTurkey);

        var englishMilestone = english.Milestones.Single(item => item.Id == "movies-10");
        var turkishMilestone = turkish.Milestones.Single(item => item.Id == "movies-10");

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
}
