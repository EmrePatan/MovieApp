using System.Diagnostics;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Infrastructure.AiRecommendations;

internal static class ExternalLlmRecommendationAttemptExecutor
{
    internal static async Task<AiExternalLlmProviderAttempt> ExecuteAsync(
        string providerName,
        Func<CancellationToken, Task<AiProviderGenerationResult>> generate,
        TimeSpan timeout,
        CancellationToken callerCancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(callerCancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            var result = await generate(timeoutCts.Token);
            stopwatch.Stop();
            return new AiExternalLlmProviderAttempt(
                providerName,
                true,
                result,
                null,
                null,
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (callerCancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            throw;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return Failure(providerName, AiProviderFailureCategory.Timeout, null, stopwatch.ElapsedMilliseconds);
        }
        catch (HttpRequestException)
        {
            stopwatch.Stop();
            return Failure(providerName, AiProviderFailureCategory.Network, null, stopwatch.ElapsedMilliseconds);
        }
        catch (AiRecommendationProviderHttpException exception)
        {
            stopwatch.Stop();
            return Failure(
                providerName,
                AiProviderHttpFailureClassifier.Classify(exception.StatusCode),
                (int)exception.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (AiRecommendationProviderException exception)
        {
            stopwatch.Stop();
            var category = exception.Message.Contains("no suggestions", StringComparison.OrdinalIgnoreCase) ||
                           exception.Message.Contains("unusable", StringComparison.OrdinalIgnoreCase) ||
                           exception.Message.Contains("empty", StringComparison.OrdinalIgnoreCase)
                ? AiProviderFailureCategory.EmptyResponse
                : AiProviderFailureCategory.MalformedResponse;

            return Failure(providerName, category, null, stopwatch.ElapsedMilliseconds);
        }
    }

    private static AiExternalLlmProviderAttempt Failure(
        string providerName,
        AiProviderFailureCategory category,
        int? httpStatus,
        long durationMs) =>
        new(providerName, false, null, category, httpStatus, durationMs);
}
