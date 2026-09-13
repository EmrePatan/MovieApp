using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;

namespace MovieApp.UnitTests.Identity;

public sealed class UserProfileServiceTests
{
    [Fact]
    public async Task GetCurrentProfileAsyncReturnsProfileForAuthenticatedUser()
    {
        var user = CreateUser();
        var service = CreateService(user);

        var result = await service.GetCurrentProfileAsync();

        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal(user.DisplayName, result.DisplayName);
    }

    [Fact]
    public async Task UpdateDisplayNameAsyncTrimsAndPersistsDisplayName()
    {
        var user = CreateUser();
        var repository = new FakeUserRepository(user);
        var service = CreateService(user, repository);

        var result = await service.UpdateDisplayNameAsync("  Updated Name  ");

        Assert.Equal("Updated Name", result.DisplayName);
        Assert.Equal("Updated Name", user.DisplayName);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public async Task UpdateDisplayNameAsyncRejectsEmptyDisplayName()
    {
        var service = CreateService(CreateUser());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateDisplayNameAsync("   "));
    }

    [Fact]
    public async Task UpdateDisplayNameAsyncRejectsTooLongDisplayName()
    {
        var service = CreateService(CreateUser());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateDisplayNameAsync(new string('a', 101)));
    }

    [Fact]
    public async Task ChangeEmailAsyncUpdatesEmailAndReturnsNewToken()
    {
        var user = CreateUser();
        var repository = new FakeUserRepository(user);
        var service = CreateService(user, repository, passwordShouldVerify: true);

        var result = await service.ChangeEmailAsync("new@example.com", "StrongPassword123");

        Assert.Equal("new@example.com", result.User.Email);
        Assert.Equal("token", result.AccessToken);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public async Task ChangeEmailAsyncNormalizesEmail()
    {
        var user = CreateUser();
        var repository = new FakeUserRepository(user);
        var service = CreateService(user, repository, passwordShouldVerify: true);

        await service.ChangeEmailAsync(" NEW@Example.com ", "StrongPassword123");

        Assert.Equal("new@example.com", user.NormalizedEmail);
    }

    [Fact]
    public async Task ChangeEmailAsyncThrowsConflictForDuplicateEmail()
    {
        var user = CreateUser();
        var otherUser = CreateUser();
        otherUser.NormalizedEmail = "other@example.com";
        var repository = new FakeUserRepository(user, existingNormalizedEmail: "other@example.com");
        var service = CreateService(user, repository, passwordShouldVerify: true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.ChangeEmailAsync("other@example.com", "StrongPassword123"));
    }

    [Fact]
    public async Task ChangeEmailAsyncRejectsIncorrectCurrentPassword()
    {
        var service = CreateService(CreateUser(), passwordShouldVerify: false);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ChangeEmailAsync("new@example.com", "WrongPassword123"));
    }

    [Fact]
    public async Task ChangePasswordAsyncUpdatesHashAndReturnsNewToken()
    {
        var user = CreateUser();
        var repository = new FakeUserRepository(user);
        var passwordHasher = new FakePasswordHasher(shouldVerifyCurrentPassword: true, newHash: "new-hash");
        var service = CreateService(user, repository, passwordHasher);

        var result = await service.ChangePasswordAsync("StrongPassword123", "AnotherPassword123");

        Assert.Equal("new-hash", user.PasswordHash);
        Assert.Equal("token", result.AccessToken);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public async Task ChangePasswordAsyncRejectsSamePassword()
    {
        var service = CreateService(CreateUser(), passwordShouldVerify: true, newPasswordMatchesCurrent: true);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ChangePasswordAsync("StrongPassword123", "AnotherPassword123"));
    }

    [Fact]
    public async Task ChangePasswordAsyncRejectsInvalidNewPassword()
    {
        var service = CreateService(CreateUser(), passwordShouldVerify: true);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ChangePasswordAsync("StrongPassword123", "short"));
    }

    [Fact]
    public async Task GetStatisticsAsyncReturnsRepositoryCounts()
    {
        var user = CreateUser();
        var statistics = CreateSampleStatistics();
        var service = CreateService(user, statistics: statistics);

        var result = await service.GetStatisticsAsync();

        Assert.Equal(statistics, result);
        Assert.Equal(9, result.Summary.MoviesWatched);
    }

    [Fact]
    public async Task DeleteAccountAsyncDeletesUserWhenPasswordIsValid()
    {
        var user = CreateUser();
        var repository = new FakeUserRepository(user);
        var service = CreateService(user, repository, passwordShouldVerify: true);

        await service.DeleteAccountAsync("StrongPassword123");

        Assert.Equal(1, repository.DeleteCount);
    }

    [Fact]
    public async Task DeleteAccountAsyncRejectsIncorrectPassword()
    {
        var service = CreateService(CreateUser(), passwordShouldVerify: false);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.DeleteAccountAsync("WrongPassword123"));
    }

    [Fact]
    public async Task GetCurrentProfileAsyncThrowsWhenUserIsInactive()
    {
        var user = CreateUser();
        user.IsActive = false;
        var service = CreateService(user);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetCurrentProfileAsync());
    }

    private static UserProfileService CreateService(
        User user,
        FakeUserRepository? repository = null,
        IPasswordHasher? passwordHasher = null,
        UserStatisticsResult? statistics = null,
        bool passwordShouldVerify = true,
        bool newPasswordMatchesCurrent = false)
    {
        repository ??= new FakeUserRepository(user);
        passwordHasher ??= new FakePasswordHasher(passwordShouldVerify, "new-hash", newPasswordMatchesCurrent);
        var statisticsRepository = new FakeUserStatisticsRepository(statistics ?? CreateEmptyStatistics());

        return new UserProfileService(
            new FakeCurrentUser(user.Id),
            repository,
            statisticsRepository,
            passwordHasher,
            new FakeTokenService());
    }

    private static User CreateUser() =>
        User.Create(
            Guid.NewGuid(),
            "user@example.com",
            "hashed-password",
            "Display Name",
            DateTime.UtcNow);

    private static UserStatisticsResult CreateEmptyStatistics() =>
        ProfileStatisticsBuilder.Build(
            new ProfileStatisticsRawData(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], [], [], [], null, null),
            DateTime.UtcNow);

    private static UserStatisticsResult CreateSampleStatistics() =>
        ProfileStatisticsBuilder.Build(
            new ProfileStatisticsRawData(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 2, 1, [], [], [], [], null, null),
            DateTime.UtcNow);

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class FakeUserRepository(User? user, string? existingNormalizedEmail = null) : IUserRepository
    {
        public int UpdateCount { get; private set; }

        public int DeleteCount { get; private set; }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(user is not null && user.Id == id ? user : null);

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(user is not null && user.Id == id ? user : null);

        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(user is not null && user.Id == id ? user.SecurityStamp : null);

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(user is not null && user.NormalizedEmail == normalizedEmail ? user : null);

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(
                existingNormalizedEmail is not null &&
                existingNormalizedEmail == normalizedEmail &&
                user?.NormalizedEmail != normalizedEmail);

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            UpdateCount++;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DeleteCount++;
            return Task.FromResult(user is not null && user.Id == id);
        }
    }

    private sealed class FakeUserStatisticsRepository(UserStatisticsResult statistics) : IUserStatisticsRepository
    {
        public Task<UserStatisticsResult> GetStatisticsAsync(
            Guid userId,
            string? timeZoneId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(statistics);
    }

    private sealed class FakePasswordHasher(
        bool shouldVerifyCurrentPassword,
        string newHash,
        bool newPasswordMatchesCurrent = false) : IPasswordHasher
    {
        public string HashPassword(string password) => newHash;

        public bool VerifyPassword(string password, string passwordHash) =>
            newPasswordMatchesCurrent ||
            (shouldVerifyCurrentPassword && password == "StrongPassword123");
    }

    private sealed class FakeTokenService : ITokenService
    {
        public AccessTokenResult CreateAccessToken(TokenUserContext user) =>
            new("token", DateTime.UtcNow.AddHours(1));
    }
}
