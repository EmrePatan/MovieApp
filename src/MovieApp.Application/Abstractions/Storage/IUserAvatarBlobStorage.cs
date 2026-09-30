namespace MovieApp.Application.Abstractions.Storage;

public interface IUserAvatarBlobStorage
{
    Task PutAsync(
        string storageKey,
        ReadOnlyMemory<byte> content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
