using MovieApp.Application.Abstractions.Storage;

namespace MovieApp.Infrastructure.Storage;

/// <summary>
/// Development-only blob storage that keeps avatars and profile covers on disk so the API can
/// serve them (see the <c>/dev-media</c> static files mapping). Used when no S3-compatible
/// storage is configured in the Development environment; never registered in Production.
/// </summary>
public sealed class LocalDiskUserAvatarBlobStorage(string rootPath) : IUserAvatarBlobStorage
{
    public static string DefaultRootPath { get; } =
        Path.Combine(Path.GetTempPath(), "movieapp-dev-media");

    public async Task PutAsync(
        string storageKey,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content.ToArray(), cancellationToken);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string storageKey)
    {
        var root = Path.GetFullPath(rootPath);
        var full = Path.GetFullPath(Path.Combine(root, storageKey.TrimStart('/', '\\')));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Storage key escapes the dev media root.");
        }

        return full;
    }
}
