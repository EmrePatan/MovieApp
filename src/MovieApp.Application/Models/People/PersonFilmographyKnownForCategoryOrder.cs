namespace MovieApp.Application.Models.People;

public static class PersonFilmographyKnownForCategoryOrder
{
    private static readonly Dictionary<string, int> PriorityByCategory =
        new(StringComparer.Ordinal)
        {
            [PersonFilmographyKnownForCategories.Movie] = 1,
            [PersonFilmographyKnownForCategories.ScriptedTelevision] = 2,
            [PersonFilmographyKnownForCategories.MiniSeries] = 3,
            [PersonFilmographyKnownForCategories.TelevisionMovie] = 4,
            [PersonFilmographyKnownForCategories.Animation] = 5,
            [PersonFilmographyKnownForCategories.Documentary] = 6,
            [PersonFilmographyKnownForCategories.TalkVarietyReality] = 7,
            [PersonFilmographyKnownForCategories.OtherTelevision] = 8,
        };

    public static int GetPriority(string knownForCategory) =>
        PriorityByCategory.TryGetValue(knownForCategory, out var priority) ? priority : int.MaxValue;
}
