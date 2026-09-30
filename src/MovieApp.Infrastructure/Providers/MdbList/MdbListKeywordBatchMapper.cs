using MovieApp.Application.Models.Keywords;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Providers.MdbList;

internal static class MdbListKeywordBatchMapper
{
    internal static Dictionary<int, IReadOnlyList<MdbListKeywordTransportItem>> MapBatchResponse(
        IReadOnlyList<MdbListBatchTitleResponseJson> payload)
    {
        var keywordsByTmdbId = new Dictionary<int, IReadOnlyList<MdbListKeywordTransportItem>>();

        foreach (var item in payload)
        {
            var tmdbId = item.Ids?.Tmdb;
            if (tmdbId is null or <= 0)
            {
                continue;
            }

            keywordsByTmdbId[tmdbId.Value] = MdbListKeywordTransportMapper.MapKeywords(item.Keywords);
        }

        return keywordsByTmdbId;
    }

    internal static MdbListKeywordTransportTelemetry MapTelemetry(MdbListFetchTelemetry telemetry) =>
        new(
            telemetry.StatusCode,
            telemetry.LatencyMilliseconds,
            telemetry.RateLimitRemaining,
            telemetry.RateLimitResetUtc);
}
