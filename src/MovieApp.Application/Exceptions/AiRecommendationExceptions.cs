namespace MovieApp.Application.Exceptions;

public sealed class AiRecommendationEntitlementException(string message)
    : Exception(message);

public sealed class AiRecommendationQuotaExceededException(string message)
    : Exception(message);

public sealed class AiRecommendationProviderUnavailableException(string message)
    : Exception(message);

public sealed class AiRecommendationInfrastructureUnavailableException(string message)
    : Exception(message);

public sealed class AiRecommendationNoValidResultsException(string message)
    : Exception(message);

public sealed class AiRecommendationProviderException(string message)
    : Exception(message);
