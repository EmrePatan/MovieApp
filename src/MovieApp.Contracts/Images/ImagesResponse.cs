namespace MovieApp.Contracts.Images;

public sealed record ImagesResponse(
    IReadOnlyList<ImageResponse> Backdrops,
    IReadOnlyList<ImageResponse> Posters,
    IReadOnlyList<ImageResponse> Logos,
    IReadOnlyList<ImageResponse> Profiles);

public sealed record ImageResponse(
    string FilePath,
    string? Language,
    decimal? AspectRatio,
    int? Width,
    int? Height,
    decimal VoteAverage,
    int VoteCount);
