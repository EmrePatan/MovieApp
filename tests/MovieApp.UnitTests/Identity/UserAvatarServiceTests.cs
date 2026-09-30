using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Storage;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;
using Microsoft.Extensions.Options;

namespace MovieApp.UnitTests.Identity;

public sealed class UserAvatarServiceTests
{
    [Fact]
    public async Task UploadRejectsOversizedDeclaredLength()
    {
        var service = CreateService(new UserRepositoryFake(User.Create(
            Guid.NewGuid(),
            "user@example.com",
            "hash",
            "User",
            DateTime.UtcNow)));

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UploadCustomAvatarAsync(Stream.Null, UserAvatarService.MaxUploadBytes + 1));
    }

    [Fact]
    public async Task UploadPersistsNewStorageKeyAndDeletesPreviousAfterSuccess()
    {
        var user = User.Create(Guid.NewGuid(), "user@example.com", "hash", "User", DateTime.UtcNow);
        user.SetCustomAvatarStorageKey("avatars/old.webp", DateTime.UtcNow);
        var repository = new UserRepositoryFake(user);
        var storage = new TrackingBlobStorage();
        var service = CreateService(repository, storage);

        await service.UploadCustomAvatarAsync(new MemoryStream([1, 2, 3]), 3);

        Assert.NotNull(user.CustomAvatarStorageKey);
        Assert.NotEqual("avatars/old.webp", user.CustomAvatarStorageKey);
        Assert.Contains(user.CustomAvatarStorageKey, storage.UploadedKeys);
        Assert.Contains("avatars/old.webp", storage.DeletedKeys);
    }

    [Fact]
    public async Task UploadDeletesOrphanWhenDatabaseUpdateFails()
    {
        var user = User.Create(Guid.NewGuid(), "user@example.com", "hash", "User", DateTime.UtcNow);
        var repository = new UserRepositoryFake(user, failUpdate: true);
        var storage = new TrackingBlobStorage();
        var service = CreateService(repository, storage);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadCustomAvatarAsync(new MemoryStream([1]), 1));

        Assert.NotEmpty(storage.UploadedKeys);
        Assert.NotEmpty(storage.DeletedKeys);
    }

    [Fact]
    public async Task RemoveClearsKeyAndDeletesObject()
    {
        var user = User.Create(Guid.NewGuid(), "user@example.com", "hash", "User", DateTime.UtcNow);
        user.SetCustomAvatarStorageKey("avatars/remove-me.webp", DateTime.UtcNow);
        var repository = new UserRepositoryFake(user);
        var storage = new TrackingBlobStorage();
        var service = CreateService(repository, storage);

        await service.RemoveCustomAvatarAsync();

        Assert.Null(user.CustomAvatarStorageKey);
        Assert.Contains("avatars/remove-me.webp", storage.DeletedKeys);
    }

    [Fact]
    public async Task RemoveCustomAvatarFallsBackToProviderPictureWhenGooglePictureExists()
    {
        const string googlePicture = "https://google.example/photo.jpg";
        var user = User.Create(Guid.NewGuid(), "user@example.com", "hash", "User", DateTime.UtcNow);
        user.SetCustomAvatarStorageKey("avatars/custom.webp", DateTime.UtcNow);
        var repository = new UserRepositoryFake(user);
        var storage = new TrackingBlobStorage();
        var service = CreateService(repository, storage, googleProviderPictureUrl: googlePicture);

        var profile = await service.RemoveCustomAvatarAsync();

        Assert.Null(user.CustomAvatarStorageKey);
        Assert.Equal(UserAvatarKind.Provider, profile.AvatarKind);
        Assert.Equal(googlePicture, profile.EffectiveAvatarUrl);
        Assert.Equal(googlePicture, profile.ProviderAvatarUrl);
        Assert.Null(profile.CustomAvatarUrl);
    }

    [Fact]
    public async Task RemoveCustomAvatarFallsBackToInitialsWhenNoProviderPicture()
    {
        var user = User.Create(Guid.NewGuid(), "user@example.com", "hash", "User", DateTime.UtcNow);
        user.SetCustomAvatarStorageKey("avatars/custom.webp", DateTime.UtcNow);
        var repository = new UserRepositoryFake(user);
        var storage = new TrackingBlobStorage();
        var service = CreateService(repository, storage);

        var profile = await service.RemoveCustomAvatarAsync();

        Assert.Null(user.CustomAvatarStorageKey);
        Assert.Equal(UserAvatarKind.Initials, profile.AvatarKind);
        Assert.Null(profile.EffectiveAvatarUrl);
    }

    [Fact]
    public async Task ImageProcessorRejectsInvalidContent()
    {
        var processor = new FakeImageProcessor(shouldFail: true);
        var user = User.Create(Guid.NewGuid(), "user@example.com", "hash", "User", DateTime.UtcNow);
        var service = CreateService(new UserRepositoryFake(user), new TrackingBlobStorage(), processor);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UploadCustomAvatarAsync(new MemoryStream([9, 9, 9]), 3));
    }

    private static UserAvatarService CreateService(
        UserRepositoryFake userRepository,
        TrackingBlobStorage? storage = null,
        IUserAvatarImageProcessor? processor = null,
        string? googleProviderPictureUrl = null)
    {
        storage ??= new TrackingBlobStorage();
        processor ??= new FakeImageProcessor();
        var presentation = new UserAvatarPresentationService(
            new AvatarReadRepositoryFake(userRepository.User, googleProviderPictureUrl),
            Options.Create(new AvatarStorageOptions { PublicBaseUrl = "https://cdn.example.com" }));

        return new UserAvatarService(
            new TestCurrentUser(userRepository.User.Id),
            userRepository,
            new FakeExternalLoginRepository(),
            processor,
            storage,
            presentation,
            NullLogger<UserAvatarService>.Instance);
    }

    private sealed class UserRepositoryFake(User user, bool failUpdate = false) : IUserRepository
    {
        public User User { get; } = user;

        public List<User> Users => [User];

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(User.Id == id ? User : null);

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            GetByIdAsync(id, cancellationToken);

        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(User.SecurityStamp);

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            if (failUpdate)
            {
                throw new InvalidOperationException("Simulated persistence failure.");
            }

            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class AvatarReadRepositoryFake(User user, string? googleProviderPictureUrl = null)
        : IUserAvatarReadRepository
    {
        public Task<UserAvatarSources?> GetSourcesForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserAvatarSources?>(
                userId == user.Id
                    ? new UserAvatarSources(user.Id, user.CustomAvatarStorageKey, googleProviderPictureUrl)
                    : null);

        public Task<IReadOnlyDictionary<Guid, UserAvatarSources>> GetSourcesForUsersAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, UserAvatarSources>>(new Dictionary<Guid, UserAvatarSources>());
    }

    private sealed class TrackingBlobStorage : IUserAvatarBlobStorage
    {
        public List<string> UploadedKeys { get; } = [];
        public List<string> DeletedKeys { get; } = [];

        public Task PutAsync(
            string storageKey,
            ReadOnlyMemory<byte> content,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            UploadedKeys.Add(storageKey);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            DeletedKeys.Add(storageKey);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeImageProcessor(bool shouldFail = false) : IUserAvatarImageProcessor
    {
        public Task<ProcessedUserAvatarImage> ProcessUploadAsync(Stream uploadStream, CancellationToken cancellationToken = default)
        {
            if (shouldFail)
            {
                throw new ValidationException("Avatar must be a JPEG, PNG, or WebP image.");
            }

            return Task.FromResult(new ProcessedUserAvatarImage([1, 2, 3]));
        }
    }

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
    }

    private sealed class FakeExternalLoginRepository : IUserExternalLoginRepository
    {
        public Task<User?> GetUserByProviderAndSubjectAsync(string provider, string providerSubject, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<UserExternalLogin> CreateAsync(UserExternalLogin externalLogin, CancellationToken cancellationToken = default) =>
            Task.FromResult(externalLogin);

        public Task<IReadOnlyList<string>> GetProvidersForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<bool> DeleteAsync(Guid userId, string provider, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task UpdateProviderPictureUrlAsync(
            Guid userId,
            string provider,
            string? pictureUrl,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
