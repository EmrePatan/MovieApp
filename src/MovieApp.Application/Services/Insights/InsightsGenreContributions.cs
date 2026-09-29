using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public static class InsightsGenreContributions
{
    public static IReadOnlyList<InsightsV3GenreContribution> FromTitles(
        IEnumerable<InsightsDnaTitleData> titles)
    {
        return titles
            .Where(title => title.Genres.Count > 0)
            .SelectMany(title => title.Genres.Select(genre => new
            {
                genre.GenreId,
                genre.Name,
                GenresOnTitle = title.Genres.Count,
            }))
            .GroupBy(item => (item.GenreId, item.Name, item.GenresOnTitle))
            .Select(group => new InsightsV3GenreContribution(
                group.Key.GenreId,
                group.Key.Name,
                group.Key.GenresOnTitle,
                group.Count()))
            .ToList();
    }

    public static Dictionary<Guid, (string Name, decimal Weight)> SumWeightsByGenreId(
        IReadOnlyList<InsightsV3GenreContribution> contributions)
    {
        var weights = new Dictionary<Guid, (string Name, decimal Weight)>();

        foreach (var row in contributions)
        {
            if (row.GenresOnTitle <= 0 || row.TitleCount <= 0)
            {
                continue;
            }

            var perTitle = 1m / row.GenresOnTitle;
            if (!weights.TryGetValue(row.GenreId, out var existing))
            {
                existing = (row.Name, 0m);
            }

            var weight = existing.Weight;
            for (var index = 0; index < row.TitleCount; index++)
            {
                weight += perTitle;
            }

            weights[row.GenreId] = (existing.Name, weight);
        }

        return weights;
    }

    public static Dictionary<(Guid GenreId, string Name), decimal> SumWeightsByGenreAndName(
        IReadOnlyList<InsightsV3GenreContribution> contributions)
    {
        var weights = new Dictionary<(Guid GenreId, string Name), decimal>();

        foreach (var row in contributions)
        {
            if (row.GenresOnTitle <= 0 || row.TitleCount <= 0)
            {
                continue;
            }

            var perTitle = 1m / row.GenresOnTitle;
            var key = (row.GenreId, row.Name);
            var weight = weights.GetValueOrDefault(key);
            for (var index = 0; index < row.TitleCount; index++)
            {
                weight += perTitle;
            }

            weights[key] = weight;
        }

        return weights;
    }
}
