using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Home;

internal static class TopRatedDiversityGuardrail
{
    public static List<SearchItem> ApplyAnimationCap(
        IReadOnlyList<SearchItem> rankedCandidates,
        IReadOnlySet<CatalogContentKey> animationContentKeys,
        int maxAnimationItems,
        int targetCount)
    {
        if (targetCount <= 0 || rankedCandidates.Count == 0)
        {
            return [];
        }

        var selected = new List<SearchItem>(Math.Min(targetCount, rankedCandidates.Count));
        var animationSelected = 0;

        foreach (var item in rankedCandidates)
        {
            if (selected.Count >= targetCount)
            {
                break;
            }

            var contentKey = new CatalogContentKey(item.Id, item.Type);
            var isAnimation = animationContentKeys.Contains(contentKey);

            if (isAnimation && animationSelected >= maxAnimationItems)
            {
                continue;
            }

            selected.Add(item);

            if (isAnimation)
            {
                animationSelected++;
            }
        }

        return selected;
    }
}
