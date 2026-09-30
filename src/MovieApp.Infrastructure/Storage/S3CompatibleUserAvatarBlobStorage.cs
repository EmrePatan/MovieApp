using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Storage;
using MovieApp.Application.Configuration;

namespace MovieApp.Infrastructure.Storage;

public sealed class S3CompatibleUserAvatarBlobStorage : IUserAvatarBlobStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucketName;

    public S3CompatibleUserAvatarBlobStorage(IOptions<AvatarStorageOptions> options)
    {
        var value = options.Value;
        _bucketName = value.BucketName.Trim();
        var config = new AmazonS3Config
        {
            ServiceURL = value.Endpoint.Trim(),
            ForcePathStyle = true,
        };
        _client = new AmazonS3Client(value.AccessKeyId.Trim(), value.SecretAccessKey.Trim(), config);
    }

    public async Task PutAsync(
        string storageKey,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(content.ToArray());
        var request = CreatePutObjectRequest(_bucketName, storageKey, stream, contentType);
        await _client.PutObjectAsync(request, cancellationToken);
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = storageKey,
        };
        await _client.DeleteObjectAsync(request, cancellationToken);
    }

    public void Dispose() => _client.Dispose();

    internal static PutObjectRequest CreatePutObjectRequest(
        string bucketName,
        string storageKey,
        Stream inputStream,
        string contentType)
    {
        return new PutObjectRequest
        {
            BucketName = bucketName,
            Key = storageKey,
            InputStream = inputStream,
            ContentType = contentType,
            AutoCloseStream = true,
            // AWSSDK.S3 v4 defaults UseChunkEncoding to true, which signs uploads as
            // STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER. Many S3-compatible endpoints reject that.
            UseChunkEncoding = false,
        };
    }
}
