namespace MovieApp.Contracts.AiRecommendations;

public sealed record AiRecommendationRequest(string Message, Guid? SessionId);
