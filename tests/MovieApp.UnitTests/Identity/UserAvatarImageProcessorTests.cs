using MovieApp.Application.Exceptions;
using MovieApp.Infrastructure.Identity;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace MovieApp.UnitTests.Identity;

public sealed class UserAvatarImageProcessorTests
{
    private readonly UserAvatarImageProcessor _processor = new();

    [Fact]
    public async Task RejectsNonImageBytes()
    {
        await using var stream = new MemoryStream("not-an-image"u8.ToArray());

        await Assert.ThrowsAsync<ValidationException>(() => _processor.ProcessUploadAsync(stream));
    }

    [Theory]
    [InlineData(new byte[] { 0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 })] // TIFF little-endian
    [InlineData(new byte[] { 0x4D, 0x4D, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00 })] // TIFF big-endian
    [InlineData(new byte[] { 0x49, 0x49, 0x2B, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 })] // BigTIFF
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00 })] // GIF
    [InlineData(new byte[] { 0x42, 0x4D, 0x1E, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 })] // BMP
    public async Task RejectsUnsupportedImageSignaturesBeforeDecoding(byte[] bytes)
    {
        await using var avatar = new MemoryStream(bytes);
        await using var cover = new MemoryStream(bytes);

        await Assert.ThrowsAsync<ValidationException>(() => _processor.ProcessUploadAsync(avatar));
        await Assert.ThrowsAsync<ValidationException>(() => _processor.ProcessCoverUploadAsync(cover));
    }

    [Fact]
    public async Task ProcessesMinimalPngToWebp()
    {
        var pngBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        await using var stream = new MemoryStream(pngBytes);

        var result = await _processor.ProcessUploadAsync(stream);

        Assert.NotEmpty(result.WebpContent);
        Assert.True(result.WebpContent.Length < 50_000);
        Assert.Equal("RIFF"u8, result.WebpContent.AsSpan(0, 4));
        Assert.Equal("WEBP"u8, result.WebpContent.AsSpan(8, 4));
    }

    [Theory]
    [InlineData("jpeg")]
    [InlineData("png")]
    [InlineData("webp")]
    public async Task ProcessesSupportedFormatsToAvatarAndCoverSizes(string format)
    {
        var bytes = CreateImage(900, 600, format);
        await using var avatar = new MemoryStream(bytes);
        await using var cover = new MemoryStream(bytes);

        var avatarResult = await _processor.ProcessUploadAsync(avatar);
        var coverResult = await _processor.ProcessCoverUploadAsync(cover);

        var avatarInfo = Image.Identify(avatarResult.WebpContent);
        Assert.Equal(
            new Size(UserAvatarImageProcessor.OutputSizePixels, UserAvatarImageProcessor.OutputSizePixels),
            avatarInfo.Size);
        var coverInfo = Image.Identify(coverResult.WebpContent);
        Assert.Equal(
            new Size(UserAvatarImageProcessor.CoverWidthPixels, UserAvatarImageProcessor.CoverHeightPixels),
            coverInfo.Size);
    }

    private static byte[] CreateImage(int width, int height, string format)
    {
        using var image = new Image<Rgba32>(width, height, Color.Red.ToPixel<Rgba32>());
        using var ms = new MemoryStream();
        switch (format)
        {
            case "jpeg":
                image.SaveAsJpeg(ms);
                break;
            case "webp":
                image.SaveAsWebp(ms);
                break;
            default:
                image.SaveAsPng(ms);
                break;
        }

        return ms.ToArray();
    }

    [Fact]
    public async Task RejectsOversizedDeclaredDimensions()
    {
        var pngBytes = CreateSolidPng(UserAvatarImageProcessor.MaxSourceDimensionPixels + 1, 8);
        await using var stream = new MemoryStream(pngBytes);

        await Assert.ThrowsAsync<ValidationException>(() => _processor.ProcessUploadAsync(stream));
    }

    private static byte[] CreateSolidPng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(ctx => ctx.BackgroundColor(Color.Red));
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }
}
