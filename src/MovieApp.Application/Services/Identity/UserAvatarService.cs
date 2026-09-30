using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Storage;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Identity;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Identity;

public sealed class UserAvatarService(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IUserExternalLoginRepository externalLoginRepository,
    IUserAvatarImageProcessor imageProcessor,
    IUserAvatarBlobStorage blobStorage,
    IUserAvatarPresentationService avatarPresentationService,
    ILogger<UserAvatarService> logger) : IUserAvatarService
{
    public const long MaxUploadBytes = 5 * 1024 * 1024;

    public async Task<UserProfileResult> UploadCustomAvatarAsync(
        Stream uploadStream,
        long? declaredContentLength,
        CancellationToken cancellationToken = default)
    {
        if (declaredContentLength is > MaxUploadBytes)
        {
            throw new ValidationException("Avatar image must be 5 MB or smaller.");
        }

        await using var boundedStream = new BoundedReadStream(uploadStream, MaxUploadBytes);
        var processed = await imageProcessor.ProcessUploadAsync(boundedStream, cancellationToken);

        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var user = await userRepository.GetByIdForUpdateAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new NotFoundException("The authenticated user was not found.");
        }

        var previousKey = user.CustomAvatarStorageKey;
        var newKey = $"avatars/{userId}/{Guid.NewGuid():N}.webp";

        try
        {
            await blobStorage.PutAsync(
                newKey,
                processed.WebpContent,
                "image/webp",
                cancellationToken);

            user.SetCustomAvatarStorageKey(newKey, DateTime.UtcNow);
            await userRepository.UpdateAsync(user, cancellationToken);
        }
        catch (Exception)
        {
            await BestEffortDeleteCustomAvatarAsync(newKey, cancellationToken);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(previousKey) &&
            !string.Equals(previousKey, newKey, StringComparison.Ordinal))
        {
            await BestEffortDeleteCustomAvatarAsync(previousKey, cancellationToken);
        }

        return await MapProfileAsync(user, cancellationToken);
    }

    public async Task<UserProfileResult> RemoveCustomAvatarAsync(CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var user = await userRepository.GetByIdForUpdateAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new NotFoundException("The authenticated user was not found.");
        }

        var previousKey = user.CustomAvatarStorageKey;
        if (string.IsNullOrWhiteSpace(previousKey))
        {
            return await MapProfileAsync(user, cancellationToken);
        }

        user.ClearCustomAvatarStorageKey(DateTime.UtcNow);
        await userRepository.UpdateAsync(user, cancellationToken);
        await BestEffortDeleteCustomAvatarAsync(previousKey, cancellationToken);

        return await MapProfileAsync(user, cancellationToken);
    }

    public async Task BestEffortDeleteCustomAvatarAsync(
        string? storageKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return;
        }

        try
        {
            await blobStorage.DeleteAsync(storageKey.Trim(), cancellationToken);
        }
        catch (Exception exception)
        {
            UserAvatarServiceLogMessages.LogCleanupFailed(logger, exception.GetType().Name);
        }
    }

    private async Task<UserProfileResult> MapProfileAsync(User user, CancellationToken cancellationToken)
    {
        var linkedProviders = await externalLoginRepository.GetProvidersForUserAsync(user.Id, cancellationToken);
        var avatar = await avatarPresentationService.GetForUserAsync(user.Id, cancellationToken);
        return UserMapper.ToUserProfileResult(user, linkedProviders, avatar);
    }

    private sealed class BoundedReadStream(Stream inner, long maxBytes) : Stream
    {
        private long _totalRead;

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            Track(read);
            return read;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            var read = await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
            Track(read);
            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken);
            Track(read);
            return read;
        }

        private void Track(int read)
        {
            if (read <= 0)
            {
                return;
            }

            _totalRead += read;
            if (_totalRead > maxBytes)
            {
                throw new ValidationException("Avatar image must be 5 MB or smaller.");
            }
        }

        public override void Flush() => inner.Flush();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
