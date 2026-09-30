using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Identity;

public sealed class GetCurrentUserServiceTests
{
    [Fact]
    public async Task GetCurrentUserAsyncReturnsEffectiveAvatarUrlFromPresentation()
    {
        const string effectiveUrl = "https://cdn.example.com/avatars/user/key.webp";
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "user@example.com", "hash", "Display Name", DateTime.UtcNow);
        var avatarService = new StubUserAvatarPresentationService(
            new UserAvatarPresentation(
                effectiveUrl,
                null,
                effectiveUrl,
                UserAvatarKind.Custom));

        var service = new GetCurrentUserService(
            new FakeCurrentUser(userId),
            new FakeUserRepository(user),
            avatarService);

        var result = await service.GetCurrentUserAsync();

        Assert.Equal(effectiveUrl, result.EffectiveAvatarUrl);
        Assert.Equal(userId, result.Id);
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
    }

    private sealed class FakeUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(user.Id == id ? user : null);

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            GetByIdAsync(id, cancellationToken);

        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(user.SecurityStamp);

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class StubUserAvatarPresentationService(UserAvatarPresentation presentation)
        : IUserAvatarPresentationService
    {
        public UserAvatarPresentation Present(UserAvatarSources sources) => presentation;

        public Task<UserAvatarPresentation> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(presentation);

        public Task<IReadOnlyDictionary<Guid, UserAvatarPresentation>> GetForUsersAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, UserAvatarPresentation>>(
                new Dictionary<Guid, UserAvatarPresentation>());
    }
}
