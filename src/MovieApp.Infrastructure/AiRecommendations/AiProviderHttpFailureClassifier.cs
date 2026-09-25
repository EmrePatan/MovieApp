using System.Net;
using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Infrastructure.AiRecommendations;

internal static class AiProviderHttpFailureClassifier
{
    internal static AiProviderFailureCategory Classify(HttpStatusCode statusCode) =>
        (int)statusCode switch
        {
            401 or 403 => AiProviderFailureCategory.AuthConfig,
            _ => AiProviderFailureCategory.HttpError
        };

    internal static bool ShouldFailover(HttpStatusCode statusCode) =>
        (int)statusCode is 408 or 429 or >= 500;
}
