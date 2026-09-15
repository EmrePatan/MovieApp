using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Services.Discovery;

namespace MovieApp.UnitTests.Discovery;

public sealed class PickSomethingSelectorTests
{
    [Fact]
    public void SelectFromBandReturnsNullForEmptyList()
    {
        var selected = PickSomethingSelector.SelectFromBand([]);

        Assert.Null(selected);
    }

    [Fact]
    public void SelectFromBandReturnsOnlyItemForSingleCandidate()
    {
        var item = CreateItem(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), 0.9m);

        var selected = PickSomethingSelector.SelectFromBand([item]);

        Assert.Equal(item.Id, selected?.Id);
    }

    [Fact]
    public void SelectFromBandChoosesFromTopBandOnly()
    {
        var topBandIds = Enumerable.Range(0, PickSomethingSelector.SelectionBandSize)
            .Select(index => Guid.Parse($"aaaaaaaa-aaaa-aaaa-aaaa-{index:D12}"))
            .ToList();
        var outsideBandId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var ranked = topBandIds
            .Select((id, index) => CreateItem(id, 1m - (index * 0.01m)))
            .Append(CreateItem(outsideBandId, 0.01m))
            .ToList();

        for (var attempt = 0; attempt < 25; attempt++)
        {
            var selected = PickSomethingSelector.SelectFromBand(ranked);
            Assert.NotNull(selected);
            Assert.Contains(selected.Id, topBandIds);
            Assert.NotEqual(outsideBandId, selected.Id);
        }
    }

    private static RecommendationItem CreateItem(Guid id, decimal score) =>
        new(
            id,
            "movie",
            $"Title {id}",
            null,
            null,
            null,
            null,
            null,
            8.0m,
            100,
            2024,
            score,
            "Reason");
}
