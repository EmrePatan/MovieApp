using System.Collections.Concurrent;
using MovieApp.Application.Abstractions.Storage;

namespace MovieApp.Infrastructure.Storage;

public sealed class InMemoryUserAvatarBlobStorage : IUserAvatarBlobStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

    public Task PutAsync(
        string storageKey,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        _objects[storageKey] = content.ToArray();
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        _objects.TryRemove(storageKey, out _);
        return Task.CompletedTask;
    }
}
