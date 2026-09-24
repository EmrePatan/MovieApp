using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MovieApp.Infrastructure.Providers.MdbList;

public sealed class MdbListApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly MdbListOptions _options;
    private readonly ILogger<MdbListApiClient> _logger;

    public MdbListApiClient(
        HttpClient httpClient,
        IOptions<MdbListOptions> options,
        ILogger<MdbListApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MdbListFetchResponse?> GetByTmdbIdAsync(
        string mediaSegment,
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured())
        {
            return null;
        }

        var relativePath = $"tmdb/{mediaSegment}/{tmdbId}?apikey={Uri.EscapeDataString(_options.ApiKey)}";
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await _httpClient.GetAsync(relativePath, cancellationToken);
            stopwatch.Stop();

            var telemetry = new MdbListFetchTelemetry(
                (int)response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                TryReadRateLimitRemaining(response),
                TryReadRateLimitReset(response));

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new MdbListFetchResponse(null, true, telemetry);
            }

            if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                MdbListApiClientLogMessages.LogNonSuccess(
                    _logger,
                    mediaSegment,
                    tmdbId,
                    (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<MdbListTitleResponseJson>(
                SerializerOptions,
                cancellationToken);

            return new MdbListFetchResponse(payload, false, telemetry);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            MdbListApiClientLogMessages.LogTransportFailure(
                _logger,
                mediaSegment,
                tmdbId,
                exception);
            return null;
        }
    }

    private static int? TryReadRateLimitRemaining(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var values)
            && int.TryParse(values.FirstOrDefault(), out var remaining))
        {
            return remaining;
        }

        return null;
    }

    private static DateTimeOffset? TryReadRateLimitReset(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var values)
            && long.TryParse(values.FirstOrDefault(), out var unixSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }

        return null;
    }
}

public sealed record MdbListFetchResponse(
    MdbListTitleResponseJson? Payload,
    bool IsNotFound,
    MdbListFetchTelemetry Telemetry);

public sealed record MdbListFetchTelemetry(
    int StatusCode,
    long LatencyMilliseconds,
    int? RateLimitRemaining,
    DateTimeOffset? RateLimitResetUtc);
