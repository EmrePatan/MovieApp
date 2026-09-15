using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;

namespace MovieApp.UnitTests.Home;

public sealed class TopRatedDiversityGuardrailTests
{
    [Fact]
    public void ApplyAnimationCapLimitsAnimationTitlesToThreeWhenAlternativesExist()
    {
        var ranked = CreateRankedCandidates(animationCount: 7, otherCount: 15);
        var animationKeys = CreateAnimationKeys(ranked);

        var result = TopRatedDiversityGuardrail.ApplyAnimationCap(
            ranked,
            animationKeys,
            maxAnimationItems: 3,
            targetCount: 10);

        Assert.Equal(10, result.Count);
        Assert.Equal(3, CountAnimation(result, animationKeys));
    }

    [Fact]
    public void ApplyAnimationCapPreservesHighestRankedAnimationTitles()
    {
        var ranked = CreateRankedCandidates(animationCount: 7, otherCount: 15);
        var animationKeys = CreateAnimationKeys(ranked);

        var result = TopRatedDiversityGuardrail.ApplyAnimationCap(
            ranked,
            animationKeys,
            maxAnimationItems: 3,
            targetCount: 10);

        Assert.Equal(
            ["animation-0", "animation-1", "animation-2"],
            result
                .Where(item => animationKeys.Contains(new CatalogContentKey(item.Id, item.Type)))
                .Select(item => item.Title)
                .ToList());
    }

    [Fact]
    public void ApplyAnimationCapPreservesNonAnimationBayesianOrder()
    {
        var ranked = CreateRankedCandidates(animationCount: 7, otherCount: 15);
        var animationKeys = CreateAnimationKeys(ranked);

        var result = TopRatedDiversityGuardrail.ApplyAnimationCap(
            ranked,
            animationKeys,
            maxAnimationItems: 3,
            targetCount: 10);

        var nonAnimation = result
            .Where(item => !animationKeys.Contains(new CatalogContentKey(item.Id, item.Type)))
            .Select(item => item.Title)
            .ToList();

        Assert.Equal(
            ["other-0", "other-1", "other-2", "other-3", "other-4", "other-5", "other-6"],
            nonAnimation);
    }

    [Fact]
    public void ApplyAnimationCapAllowsHighQualityAnimationAtRankOne()
    {
        var ranked = CreateRankedCandidates(animationCount: 7, otherCount: 15);
        var animationKeys = CreateAnimationKeys(ranked);

        var result = TopRatedDiversityGuardrail.ApplyAnimationCap(
            ranked,
            animationKeys,
            maxAnimationItems: 3,
            targetCount: 10);

        Assert.Equal("animation-0", result[0].Title);
        Assert.Contains(new CatalogContentKey(result[0].Id, result[0].Type), animationKeys);
    }

    [Fact]
    public void ApplyAnimationCapFillsRailByContinuingDownRankedList()
    {
        var ranked = CreateRankedCandidates(animationCount: 7, otherCount: 5);
        var animationKeys = CreateAnimationKeys(ranked);

        var result = TopRatedDiversityGuardrail.ApplyAnimationCap(
            ranked,
            animationKeys,
            maxAnimationItems: 3,
            targetCount: 10);

        Assert.Equal(8, result.Count);
        Assert.Equal(3, CountAnimation(result, animationKeys));
        Assert.Equal("other-4", result[^1].Title);
    }

    [Fact]
    public void ApplyAnimationCapIsDeterministic()
    {
        var ranked = CreateRankedCandidates(animationCount: 7, otherCount: 15);
        var animationKeys = CreateAnimationKeys(ranked);

        var first = TopRatedDiversityGuardrail.ApplyAnimationCap(
            ranked,
            animationKeys,
            maxAnimationItems: 3,
            targetCount: 10);
        var second = TopRatedDiversityGuardrail.ApplyAnimationCap(
            ranked,
            animationKeys,
            maxAnimationItems: 3,
            targetCount: 10);

        Assert.Equal(first.Select(item => item.Id), second.Select(item => item.Id));
    }

    private static List<SearchItem> CreateRankedCandidates(int animationCount, int otherCount)
    {
        var ranked = new List<SearchItem>(animationCount + otherCount);

        for (var index = 0; index < animationCount; index++)
        {
            ranked.Add(CreateItem($"animation-{index}", "movie", index));
        }

        for (var index = 0; index < otherCount; index++)
        {
            ranked.Add(CreateItem($"other-{index}", "movie", animationCount + index));
        }

        return ranked;
    }

    private static SearchItem CreateItem(string title, string type, int rank)
    {
        var seed = rank + 1;
        return new SearchItem(
            Guid.Parse($"bbbbbbbb-bbbb-bbbb-bbbb-{seed:D012}"),
            type,
            title,
            null,
            null,
            null,
            null,
            new DateOnly(2020, 1, 1),
            10m - (rank * 0.1m),
            1000 - rank,
            2020);
    }

    private static HashSet<CatalogContentKey> CreateAnimationKeys(IEnumerable<SearchItem> items) =>
        items
            .Where(item => item.Title.StartsWith("animation", StringComparison.Ordinal))
            .Select(item => new CatalogContentKey(item.Id, item.Type))
            .ToHashSet();

    private static int CountAnimation(
        IReadOnlyList<SearchItem> items,
        HashSet<CatalogContentKey> animationKeys) =>
        items.Count(item => animationKeys.Contains(new CatalogContentKey(item.Id, item.Type)));
}
