using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;

namespace MovieApp.UnitTests.Identity;

public sealed class ExternalLoginPictureSyncTests
{
    [Fact]
    public async Task RefreshGooglePictureUpdatesRepository()
    {
        var repository = new TrackingExternalLoginRepository();
        var userId = Guid.NewGuid();
        var identity = new VerifiedSocialIdentity(
            ExternalLoginProviders.Google,
            "subject",
            "user@example.com",
            true,
            "User",
            "https://google.example/avatar.jpg");

        await ExternalLoginPictureSync.RefreshGooglePictureIfPresentAsync(
            repository,
            userId,
            identity,
            CancellationToken.None);

        Assert.Equal("https://google.example/avatar.jpg", repository.LastPictureUrl);
        Assert.Equal(ExternalLoginProviders.Google, repository.LastProvider);
        Assert.Equal(userId, repository.LastUserId);
    }

    [Fact]
    public async Task AppleIdentityDoesNotUpdateRepository()
    {
        var repository = new TrackingExternalLoginRepository();
        var identity = new VerifiedSocialIdentity(
            ExternalLoginProviders.Apple,
            "subject",
            "user@example.com",
            true,
            null,
            null);

        await ExternalLoginPictureSync.RefreshGooglePictureIfPresentAsync(
            repository,
            Guid.NewGuid(),
            identity,
            CancellationToken.None);

        Assert.Null(repository.LastPictureUrl);
    }

    private sealed class TrackingExternalLoginRepository : IUserExternalLoginRepository
    {
        public Guid? LastUserId { get; private set; }
        public string? LastProvider { get; private set; }
        public string? LastPictureUrl { get; private set; }

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
            CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            LastProvider = provider;
            LastPictureUrl = pictureUrl;
            return Task.CompletedTask;
        }
    }
}
