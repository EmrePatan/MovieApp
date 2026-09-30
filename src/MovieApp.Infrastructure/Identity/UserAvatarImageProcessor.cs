using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Exceptions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace MovieApp.Infrastructure.Identity;

public sealed class UserAvatarImageProcessor : IUserAvatarImageProcessor
{
    public const int OutputSizePixels = 512;

    /// <summary>Maximum width or height allowed on the decoded source image.</summary>
    public const int MaxSourceDimensionPixels = 4096;

    /// <summary>Hard cap on decoded pixel count (width × height).</summary>
    public const long MaxDecodedPixels = 16_777_216;

    private const int TargetWebpQuality = 80;

    private static readonly DecoderOptions IdentifyOptions = new()
    {
        MaxFrames = 1,
        SkipMetadata = true,
    };

    private static DecoderOptions CreateLoadOptions() => new()
    {
        MaxFrames = 1,
        SkipMetadata = true,
        TargetSize = new Size(MaxSourceDimensionPixels, MaxSourceDimensionPixels),
    };

    public async Task<ProcessedUserAvatarImage> ProcessUploadAsync(
        Stream uploadStream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var buffer = await CopyToMemoryStreamAsync(uploadStream, cancellationToken);
            if (buffer.Length == 0)
            {
                throw new ValidationException("Avatar must be a JPEG, PNG, or WebP image.");
            }

            buffer.Position = 0;
            var imageInfo = await Image.IdentifyAsync(IdentifyOptions, buffer, cancellationToken);
            if (imageInfo is null)
            {
                throw new ValidationException("Avatar must be a JPEG, PNG, or WebP image.");
            }

            EnsureSupportedFormat(imageInfo.Metadata.DecodedImageFormat);
            EnsureSourceDimensionsWithinLimits(imageInfo.Width, imageInfo.Height);

            buffer.Position = 0;
            using var image = await Image.LoadAsync(CreateLoadOptions(), buffer, cancellationToken);

            image.Mutate(context =>
            {
                context.AutoOrient();
                var size = Math.Min(image.Width, image.Height);
                var x = (image.Width - size) / 2;
                var y = (image.Height - size) / 2;
                context.Crop(new Rectangle(x, y, size, size));
                context.Resize(OutputSizePixels, OutputSizePixels);
            });

            await using var output = new MemoryStream();
            var encoder = new WebpEncoder
            {
                Quality = TargetWebpQuality,
                Method = WebpEncodingMethod.Default,
            };
            await image.SaveAsWebpAsync(output, encoder, cancellationToken);
            return new ProcessedUserAvatarImage(output.ToArray());
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (UnknownImageFormatException)
        {
            throw new ValidationException("Avatar must be a JPEG, PNG, or WebP image.");
        }
        catch (InvalidImageContentException)
        {
            throw new ValidationException("Avatar must be a JPEG, PNG, or WebP image.");
        }
        catch (ImageFormatException)
        {
            throw new ValidationException("Avatar must be a JPEG, PNG, or WebP image.");
        }
    }

    private static void EnsureSupportedFormat(IImageFormat? format)
    {
        if (format is JpegFormat or PngFormat or WebpFormat)
        {
            return;
        }

        throw new ValidationException("Avatar must be a JPEG, PNG, or WebP image.");
    }

    private static void EnsureSourceDimensionsWithinLimits(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ValidationException("Avatar must be a JPEG, PNG, or WebP image.");
        }

        if (width > MaxSourceDimensionPixels || height > MaxSourceDimensionPixels)
        {
            throw new ValidationException("Avatar image dimensions are too large.");
        }

        if ((long)width * height > MaxDecodedPixels)
        {
            throw new ValidationException("Avatar image dimensions are too large.");
        }
    }

    private static async Task<MemoryStream> CopyToMemoryStreamAsync(
        Stream uploadStream,
        CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        await uploadStream.CopyToAsync(buffer, cancellationToken);
        return buffer;
    }
}
