namespace MovieApp.Application.Models.AiRecommendations;

public enum AiProviderFailureCategory
{
    NotConfigured,
    Timeout,
    Network,
    HttpError,
    AuthConfig,
    MalformedResponse,
    EmptyResponse,
    Cancelled
}
