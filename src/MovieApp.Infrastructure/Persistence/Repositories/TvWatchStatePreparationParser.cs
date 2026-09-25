using System.Text.Json;
using System.Text.Json.Serialization;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static class TvWatchStatePreparationParser
{
    private static readonly JsonSerializerOptions SeasonJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    internal static List<RegularSeasonRow> ParseRegularSeasons(string seasonsJson)
    {
        if (string.IsNullOrWhiteSpace(seasonsJson) || seasonsJson == "[]")
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<RegularSeasonRow>>(seasonsJson, SeasonJsonOptions) ?? [];
    }

    internal static (bool IngestionRequired, IReadOnlyList<int> MissingSeasonNumbers, int SeasonsWithEpisodeRowsCount)
        EvaluateIngestion(IReadOnlyList<RegularSeasonRow> seasons)
    {
        if (seasons.Count == 0)
        {
            return (true, [], 0);
        }

        var seasonsWithEpisodeRowsCount = seasons.Count(season => season.HasEpisodes);
        var missingSeasonNumbers = seasons
            .Where(season => season.SeasonNumber >= 1)
            .Where(season => season.EpisodeCount != 0 && !season.HasEpisodes)
            .Select(season => season.SeasonNumber)
            .OrderBy(seasonNumber => seasonNumber)
            .ToList();

        return (missingSeasonNumbers.Count > 0, missingSeasonNumbers, seasonsWithEpisodeRowsCount);
    }

    internal sealed class RegularSeasonRow
    {
        [JsonPropertyName("season_number")]
        public int SeasonNumber { get; set; }

        [JsonPropertyName("episode_count")]
        public int? EpisodeCount { get; set; }

        [JsonPropertyName("has_episodes")]
        public bool HasEpisodes { get; set; }
    }
}
