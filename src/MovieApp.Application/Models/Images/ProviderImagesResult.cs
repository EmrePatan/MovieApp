namespace MovieApp.Application.Models.Images;

public sealed record ProviderImageResult(
    string FilePath,
    string? Language,
    decimal? AspectRatio,
    int? Width,
    int? Height,
    decimal VoteAverage,
    int VoteCount);

public sealed record ProviderImagesResult(
    IReadOnlyList<ProviderImageResult> Backdrops,
    IReadOnlyList<ProviderImageResult> Posters,
    IReadOnlyList<ProviderImageResult> Logos,
    IReadOnlyList<ProviderImageResult> Profiles)
{
    public static ProviderImagesResult Empty => new([], [], [], []);
}
