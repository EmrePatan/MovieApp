using Amazon.S3.Model;
using MovieApp.Infrastructure.Storage;

namespace MovieApp.UnitTests.Infrastructure;

public sealed class S3CompatibleUserAvatarBlobStorageTests
{
    [Fact]
    public void CreatePutObjectRequestUsesExpectedBucketKeyContentTypeAndDisablesChunkEncoding()
    {
        var bytes = new byte[] { 0xff, 0xd8, 0xff, 0x00 };
        using var stream = new MemoryStream(bytes);

        var request = S3CompatibleUserAvatarBlobStorage.CreatePutObjectRequest(
            bucketName: "movieapp-avatars",
            storageKey: "avatars/user/abc.webp",
            inputStream: stream,
            contentType: "image/webp");

        Assert.Equal("movieapp-avatars", request.BucketName);
        Assert.Equal("avatars/user/abc.webp", request.Key);
        Assert.Equal("image/webp", request.ContentType);
        Assert.True(request.AutoCloseStream);
        Assert.False(request.UseChunkEncoding);
        Assert.Same(stream, request.InputStream);
    }

    [Fact]
    public void CreatePutObjectRequestPreservesUploadBytesOnInputStream()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        using var stream = new MemoryStream(bytes);

        var request = S3CompatibleUserAvatarBlobStorage.CreatePutObjectRequest(
            bucketName: "bucket",
            storageKey: "avatars/user/test.webp",
            inputStream: stream,
            contentType: "image/webp");

        Assert.NotNull(request.InputStream);
        using var reader = new MemoryStream();
        request.InputStream!.CopyTo(reader);
        Assert.Equal(bytes, reader.ToArray());
    }

    [Fact]
    public void CreatePutObjectRequestDoesNotUseLegacyStreamingPayloadTrailerMode()
    {
        using var stream = new MemoryStream([9]);

        var request = S3CompatibleUserAvatarBlobStorage.CreatePutObjectRequest(
            bucketName: "bucket",
            storageKey: "key",
            inputStream: stream,
            contentType: "image/jpeg");

        Assert.False(request.UseChunkEncoding);
        Assert.Null(request.DisablePayloadSigning);
    }
}
