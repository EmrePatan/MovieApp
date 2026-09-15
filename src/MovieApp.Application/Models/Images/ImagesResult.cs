namespace MovieApp.Application.Models.Images;

public sealed record ImageResult(
    string FilePath,
    string? Language,
    decimal? AspectRatio,
    int? Width,
    int? Height,
    decimal VoteAverage,
    int VoteCount);

public sealed record ImagesResult(
    IReadOnlyList<ImageResult> Backdrops,
    IReadOnlyList<ImageResult> Posters,
    IReadOnlyList<ImageResult> Logos,
    IReadOnlyList<ImageResult> Profiles)
{
    public static ImagesResult Empty => new([], [], [], []);
}
