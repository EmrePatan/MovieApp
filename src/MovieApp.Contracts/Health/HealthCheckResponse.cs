namespace MovieApp.Contracts.Health;

public sealed record HealthCheckResponse(
    string Status,
    DateTimeOffset Timestamp,
    string Environment,
    string SourceVersion);
