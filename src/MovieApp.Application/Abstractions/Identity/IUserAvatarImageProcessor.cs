namespace MovieApp.Application.Abstractions.Identity;

public interface IUserAvatarImageProcessor
{
    /// <summary>
    /// Validates and normalizes an uploaded avatar to WebP (512x512).
    /// </summary>
    Task<ProcessedUserAvatarImage> ProcessUploadAsync(Stream uploadStream, CancellationToken cancellationToken = default);
}

public sealed record ProcessedUserAvatarImage(byte[] WebpContent);
