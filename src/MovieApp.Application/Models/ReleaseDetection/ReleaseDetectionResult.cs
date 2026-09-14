namespace MovieApp.Application.Models.ReleaseDetection;

public sealed record ReleaseDetectionResult(
    int EventsCreated,
    int EventsAlreadyExisted,
    int EpisodeEventsCreated,
    int SeasonPremiereEventsCreated,
    IReadOnlyList<Guid> CreatedEventIds)
{
    public static ReleaseDetectionResult Empty { get; } = new(0, 0, 0, 0, []);
}
