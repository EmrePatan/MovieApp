namespace MovieApp.Application.Models.ReleaseDetection;

public sealed record CatalogReleaseEventInsertResult(
    int EventsCreated,
    int EventsAlreadyExisted,
    IReadOnlyList<Guid> CreatedEventIds)
{
    public static CatalogReleaseEventInsertResult Empty { get; } = new(0, 0, []);
}
