using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbApiClient
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(1000),
        TimeSpan.FromMilliseconds(2000)
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient;
    private readonly TmdbOptions _options;

    public TmdbApiClient(
        HttpClient httpClient,
        IOptions<TmdbOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<TResponse?> GetAsync<TResponse>(
        string relativePath,
        CancellationToken cancellationToken = default)
        where TResponse : class
    {
        var requestUri = BuildRequestUri(relativePath);

        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            HttpResponseMessage response;

            try
            {
                using var request = CreateRequest(requestUri);
                response = await _httpClient.SendAsync(request, cancellationToken);
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw CreateTransientAvailabilityException("TMDB request timed out.", exception);
            }
            catch (HttpRequestException exception)
            {
                throw CreateTransientAvailabilityException("TMDB service is temporarily unavailable.", exception);
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<TResponse>(SerializerOptions, cancellationToken);
                }

                if (ShouldRetry(response.StatusCode) && attempt < RetryDelays.Length)
                {
                    var delay = GetRetryDelay(response, attempt);
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                throw CreateApiException(response.StatusCode);
            }
        }

        throw CreateTransientAvailabilityException("TMDB request failed after retries.");
    }

    private HttpRequestMessage CreateRequest(string requestUri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        if (_options.UsesBearerAuthentication())
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                _options.ReadAccessToken);
        }

        return request;
    }

    private string BuildRequestUri(string relativePath)
    {
        if (_options.UsesBearerAuthentication())
        {
            return relativePath;
        }

        var separator = relativePath.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{relativePath}{separator}api_key={Uri.EscapeDataString(_options.ApiKey)}";
    }

    private static bool ShouldRetry(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests &&
            response.Headers.RetryAfter?.Delta is TimeSpan retryAfter)
        {
            return retryAfter;
        }

        return RetryDelays[attempt];
    }

    private static TmdbApiException CreateApiException(HttpStatusCode statusCode)
    {
        var message = statusCode switch
        {
            HttpStatusCode.Unauthorized =>
                "TMDB authentication failed. Verify the configured API credentials for your licensed account.",
            HttpStatusCode.Forbidden =>
                "TMDB denied the request. Verify that the configured account has access for this operation.",
            HttpStatusCode.NotFound =>
                "TMDB resource was not found.",
            HttpStatusCode.TooManyRequests =>
                "TMDB rate limit exceeded.",
            _ when (int)statusCode >= 500 =>
                "TMDB service is temporarily unavailable.",
            _ =>
                "TMDB request failed."
        };

        return new TmdbApiException(statusCode, message);
    }

    private static TmdbApiException CreateTransientAvailabilityException(
        string message,
        Exception? innerException = null) =>
        new(HttpStatusCode.ServiceUnavailable, message, innerException);
}
