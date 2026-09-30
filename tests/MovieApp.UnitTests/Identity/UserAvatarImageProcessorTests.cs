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
