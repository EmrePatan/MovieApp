using MovieApp.LoadTestIdentityProvisioner;

namespace MovieApp.LoadTestIdentityProvisioner.Tests;

public sealed class LoadTestStageTokenRequirementsTests
{
    [Fact]
    public void RecommendedMinMinutesIsThirtyForFiftyVuCapacityStage()
    {
        var min = LoadTestStageTokenRequirements.RecommendedMinMinutesUntilStageStart(50);
        Assert.InRange(min, 30, 35);
        Assert.True(min < 75);
    }

    [Fact]
    public void MintSpreadForFiftyIdentitiesUsesFifteenSecondThrottle()
    {
        var spread = LoadTestStageTokenRequirements.MintSpreadMinutes(50);
        Assert.Equal(13, spread);
    }

    [Fact]
    public void HundredIdentityMintLeavesEnoughLifetimeForCapacityStage()
    {
        var remaining = LoadTestStageTokenRequirements.OldestTokenRemainingMinutesAfterFullMint(100);
        var required = LoadTestStageTokenRequirements.CapacityStageDurationMinutes
            + LoadTestStageTokenRequirements.PreflightAndReportMarginMinutes;
        Assert.True(remaining >= required + 5);
    }

    [Fact]
    public void IdentityReuseRatio_Documents150And250StagesWithHundredIdentities()
    {
        Assert.Equal(1.5, LoadTestStageTokenRequirements.IdentityReuseRatio(150, 100), 3);
        Assert.Equal(2.5, LoadTestStageTokenRequirements.IdentityReuseRatio(250, 100), 3);
    }
}
