using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Keywords;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Providers.MdbList;

public sealed class MdbListKeywordTransportProvider(MdbListApiClient apiClient) : IMdbListKeywordTransportProvider
{
    private static readonly string[] KeywordAppend = ["keyword"];

    public async Task<MdbListKeywordsTransportResult?> FetchKeywordsAsync(
        CatalogContentType mediaType,
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        var mediaSegment = mediaType == CatalogContentType.Movie ? "movie" : "show";
        var response = await apiClient.GetByTmdbIdAsync(
            mediaSegment,
            tmdbId,
            KeywordAppend,
            cancellationToken);

        if (response is null)
        {
            return null;
        }

        if (response.IsNotFound)
        {
            return new MdbListKeywordsTransportResult(
                mediaType,
                tmdbId,
                [],
                IsNotFound: true);
        }

        var keywords = MdbListKeywordTransportMapper.MapKeywords(response.Payload?.Keywords);
        return new MdbListKeywordsTransportResult(
            mediaType,
            tmdbId,
            keywords,
            IsNotFound: false);
    }

    public async Task<MdbListKeywordsBatchTransportResult?> FetchKeywordsBatchAsync(
        CatalogContentType mediaType,
        IReadOnlyList<int> tmdbIds,
        CancellationToken cancellationToken = default)
    {
        if (tmdbIds.Count == 0)
        {
            return new MdbListKeywordsBatchTransportResult(
                mediaType,
                new Dictionary<int, IReadOnlyList<MdbListKeywordTransportItem>>(),
                new MdbListKeywordTransportTelemetry(200, 0, null, null));
        }

        var mediaSegment = mediaType == CatalogContentType.Movie ? "movie" : "show";
        var response = await apiClient.PostTmdbMediaInfoBatchAsync(
            mediaSegment,
            tmdbIds,
            KeywordAppend,
            cancellationToken);

        if (response is null)
        {
            return null;
        }

        var keywordsByTmdbId = MdbListKeywordBatchMapper.MapBatchResponse(response.Payload);
        return new MdbListKeywordsBatchTransportResult(
            mediaType,
            keywordsByTmdbId,
            MdbListKeywordBatchMapper.MapTelemetry(response.Telemetry));
    }
}
