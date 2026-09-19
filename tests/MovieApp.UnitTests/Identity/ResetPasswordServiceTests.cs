using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;

namespace MovieApp.UnitTests.Identity;

public sealed class ResetPasswordServiceTests
{
    [Fact]
    public async Task ResetPasswordAsyncUpdatesPasswordMarksTokenUsedAndRotatesSecurityStamp()
    {
        var user = CreateUser();
        var originalStamp = user.SecurityStamp;
        const string rawToken = "reset-token-value";
        var resetToken = CreateActiveToken(user.Id, rawToken);
        var tokenRepository = new FakePasswordResetTokenRepository(resetToken);
        var userRepository = new FakeUserRepository(user);
        var service = CreateService(userRepository, tokenRepository, new FakePasswordHasher());

        var result = await service.ResetPasswordAsync(
            new ResetPasswordRequest(rawToken, "AnotherPassword123"));

        Assert.Equal(ResetPasswordService.SuccessMessage, result.Message);
        Assert.Equal("new-hash", user.PasswordHash);
        Assert.NotEqual(originalStamp, user.SecurityStamp);
        Assert.NotNull(resetToken.UsedAtUtc);
        Assert.Equal(1, tokenRepository.InvalidationCount);
        Assert.Equal(1, userRepository.UpdateCount);
    }

    [Fact]
    public async Task ResetPasswordAsyncThrowsForInvalidToken()
    {
        var service = CreateService(
            new FakeUserRepository(CreateUser()),
            new FakePasswordResetTokenRepository(null),
            new FakePasswordHasher());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ResetPasswordAsync(new ResetPasswordRequest("missing-token", "AnotherPassword123")));
    }

    [Fact]
    public async Task ResetPasswordAsyncThrowsForExpiredToken()
    {
        var user = CreateUser();
        var resetToken = CreateActiveToken(user.Id, "expired-token");
        resetToken.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        var service = CreateService(
            new FakeUserRepository(user),
            new FakePasswordResetTokenRepository(resetToken),
            new FakePasswordHasher());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ResetPasswordAsync(new ResetPasswordRequest("expired-token", "AnotherPassword123")));
    }

    [Fact]
    public async Task ResetPasswordAsyncThrowsForUsedToken()
    {
        var user = CreateUser();
        var resetToken = CreateActiveToken(user.Id, "used-token");
        resetToken.UsedAtUtc = DateTime.UtcNow.AddMinutes(-5);
        var service = CreateService(
            new FakeUserRepository(user),
            new FakePasswordResetTokenRepository(resetToken),
            new FakePasswordHasher());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ResetPasswordAsync(new ResetPasswordRequest("used-token", "AnotherPassword123")));
    }

    [Fact]
    public async Task ResetPasswordAsyncThrowsForWeakPassword()
    {
        var user = CreateUser();
        var resetToken = CreateActiveToken(user.Id, "valid-token");
        var service = CreateService(
            new FakeUserRepository(user),
            new FakePasswordResetTokenRepository(resetToken),
            new FakePasswordHasher());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ResetPasswordAsync(new ResetPasswordRequest("valid-token", "short")));
    }

    [Fact]
    public async Task ResetPasswordAsyncThrowsWhenNewPasswordMatchesCurrentPasswordWithoutConsumingToken()
    {
        var user = CreateUser();
        var resetToken = CreateActiveToken(user.Id, "valid-token");
        var tokenRepository = new FakePasswordResetTokenRepository(resetToken);
        var service = CreateService(
            new FakeUserRepository(user),
            tokenRepository,
            new FakePasswordHasher(newPasswordMatchesCurrent: true));

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ResetPasswordAsync(new ResetPasswordRequest("valid-token", "StrongPassword123")));

        Assert.Null(resetToken.UsedAtUtc);
        Assert.Equal(0, tokenRepository.InvalidationCount);
    }

    [Fact]
    public async Task TryConsumeActiveTokenAsyncAllowsOnlyOneSuccessfulConsumption()
    {
        var resetToken = CreateActiveToken(Guid.NewGuid(), "single-use-token");
        var repository = new FakePasswordResetTokenRepository(resetToken);
        var utcNow = DateTime.UtcNow;

        var first = await repository.TryConsumeActiveTokenAsync(
            resetToken.TokenHash,
            utcNow);
        var second = await repository.TryConsumeActiveTokenAsync(
            resetToken.TokenHash,
            utcNow);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    private static ResetPasswordService CreateService(
        FakeUserRepository userRepository,
        FakePasswordResetTokenRepository tokenRepository,
        FakePasswordHasher passwordHasher)
    {
        var applicationDbContext = new FakeApplicationDbContext(tokenRepository);
        return new ResetPasswordService(
            applicationDbContext,
            userRepository,
            tokenRepository,
            passwordHasher);
    }

    private static PasswordResetToken CreateActiveToken(Guid userId, string rawToken) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = PasswordResetTokenHasher.HashToken(rawToken),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(55)
        };

    private static User CreateUser() =>
        User.Create(
            Guid.NewGuid(),
            "user@example.com",
            "hashed-password",
            "Display Name",
            DateTime.UtcNow);

    private sealed class FakeApplicationDbContext(FakePasswordResetTokenRepository tokenRepository)
        : IApplicationDbContext
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public async Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default)
        {
            tokenRepository.BeginTransaction();
            try
            {
                await action(cancellationToken);
                tokenRepository.CommitTransaction();
            }
            catch
            {
                tokenRepository.RollbackTransaction();
                throw;
            }
        }
    }

    private sealed class FakeUserRepository(User user) : IUserRepository
    {
        public int UpdateCount { get; private set; }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(user.Id == id ? user : null);

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(user.Id == id ? user : null);

        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(user.Id == id ? user.SecurityStamp : null);

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            UpdateCount++;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakePasswordResetTokenRepository(PasswordResetToken? activeToken) : IPasswordResetTokenRepository
    {
        private DateTime? _pendingUsedAtUtc;

        public int InvalidationCount { get; private set; }

        public void BeginTransaction() => _pendingUsedAtUtc = null;

        public void CommitTransaction()
        {
            if (activeToken is not null && _pendingUsedAtUtc is not null)
            {
                activeToken.UsedAtUtc = _pendingUsedAtUtc;
            }
        }

        public void RollbackTransaction() => _pendingUsedAtUtc = null;

        public Task<PasswordResetToken?> GetActiveByTokenHashAsync(
            string tokenHash,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            if (activeToken is null ||
                activeToken.TokenHash != tokenHash ||
                activeToken.UsedAtUtc is not null ||
                activeToken.ExpiresAtUtc <= utcNow)
            {
                return Task.FromResult<PasswordResetToken?>(null);
            }

            return Task.FromResult<PasswordResetToken?>(activeToken);
        }

        public Task<PasswordResetTokenConsumptionResult?> TryConsumeActiveTokenAsync(
            string tokenHash,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            if (activeToken is null ||
                activeToken.TokenHash != tokenHash ||
                activeToken.UsedAtUtc is not null ||
                activeToken.ExpiresAtUtc <= utcNow)
            {
                return Task.FromResult<PasswordResetTokenConsumptionResult?>(null);
            }

            if (_pendingUsedAtUtc is not null)
            {
                return Task.FromResult<PasswordResetTokenConsumptionResult?>(null);
            }

            _pendingUsedAtUtc = utcNow;
            return Task.FromResult<PasswordResetTokenConsumptionResult?>(
                new PasswordResetTokenConsumptionResult(activeToken.Id, activeToken.UserId));
        }

        public Task CreateAsync(PasswordResetToken token, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InvalidateActiveTokensForUserAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            InvalidationCount++;
            return Task.CompletedTask;
        }

        public Task<PasswordResetDeliveryTarget?> GetDeliveryTargetAsync(
            Guid tokenId,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PasswordResetDeliveryTarget?>(null);

        public Task CompleteDeliveryAsync(
            Guid tokenId,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakePasswordHasher(bool newPasswordMatchesCurrent = false) : IPasswordHasher
    {
        public string HashPassword(string password) => "new-hash";

        public bool VerifyPassword(string password, string passwordHash) =>
            newPasswordMatchesCurrent || password == "StrongPassword123";
    }
}
