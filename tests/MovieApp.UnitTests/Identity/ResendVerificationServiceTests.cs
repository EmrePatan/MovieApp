using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Identity;

public sealed class ResendVerificationServiceTests
{
    [Fact]
    public async Task ResendVerificationAsyncReturnsSameMessageForExistingAndMissingEmail()
    {
        var existingUser = CreateUnverifiedUser();
        var existingService = CreateService(new FakeUserRepository(existingUser), new RecordingEnqueuer());
        var missingService = CreateService(new FakeUserRepository(null), new RecordingEnqueuer());

        var existingResult = await existingService.ResendVerificationAsync(
            new ResendVerificationRequest("user@example.com"));
        var missingResult = await missingService.ResendVerificationAsync(
            new ResendVerificationRequest("missing@example.com"));

        Assert.Equal(ResendVerificationService.SuccessMessage, existingResult.Message);
        Assert.Equal(existingResult.Message, missingResult.Message);
    }

    [Fact]
    public async Task ResendVerificationAsyncCreatesHashedTokenAndEnqueuesDeliveryForUnverifiedPasswordUser()
    {
        var user = CreateUnverifiedUser();
        var tokenRepository = new FakeEmailVerificationTokenRepository();
        var enqueuer = new RecordingEnqueuer();
        var service = CreateService(new FakeUserRepository(user), enqueuer, tokenRepository);

        await service.ResendVerificationAsync(new ResendVerificationRequest("user@example.com"));

        Assert.Single(tokenRepository.CreatedTokens);
        Assert.Single(enqueuer.EnqueuedTokenIds);
        Assert.Equal(tokenRepository.CreatedTokens[0].Id, enqueuer.EnqueuedTokenIds[0]);
        Assert.NotNull(tokenRepository.CreatedTokens[0].ProtectedDeliverySecret);
    }

    [Fact]
    public async Task ResendVerificationAsyncInvalidatesPreviousActiveTokens()
    {
        var user = CreateUnverifiedUser();
        var tokenRepository = new FakeEmailVerificationTokenRepository();
        var service = CreateService(new FakeUserRepository(user), new RecordingEnqueuer(), tokenRepository);

        await service.ResendVerificationAsync(new ResendVerificationRequest("user@example.com"));
        await service.ResendVerificationAsync(new ResendVerificationRequest("user@example.com"));

        Assert.Equal(2, tokenRepository.CreatedTokens.Count);
        Assert.Equal(2, tokenRepository.InvalidationCount);
    }

    [Fact]
    public async Task ResendVerificationAsyncDoesNotSendEmailForVerifiedUser()
    {
        var user = CreateUnverifiedUser();
        user.MarkEmailVerified(DateTime.UtcNow);
        var enqueuer = new RecordingEnqueuer();
        var service = CreateService(new FakeUserRepository(user), enqueuer);

        await service.ResendVerificationAsync(new ResendVerificationRequest("user@example.com"));

        Assert.Empty(enqueuer.EnqueuedTokenIds);
    }

    private static ResendVerificationService CreateService(
        FakeUserRepository userRepository,
        RecordingEnqueuer enqueuer,
        FakeEmailVerificationTokenRepository? tokenRepository = null) =>
        new(
            userRepository,
            tokenRepository ?? new FakeEmailVerificationTokenRepository(),
            new PassthroughProtector(),
            enqueuer,
            Options.Create(new EmailVerificationOptions
            {
                TokenLifetimeMinutes = 1440,
                BaseUrl = "movieapp://verify-email"
            }),
            NullLogger<ResendVerificationService>.Instance);

    private static User CreateUnverifiedUser() =>
        User.Create(
            Guid.NewGuid(),
            "user@example.com",
            "hashed-password",
            "Display Name",
            DateTime.UtcNow);

    private sealed class PassthroughProtector : IEmailVerificationDeliverySecretProtector
    {
        public string Protect(string rawToken) => rawToken;

        public string Unprotect(string protectedPayload) => protectedPayload;
    }

    private sealed class RecordingEnqueuer : IEmailVerificationDeliveryEnqueuer
    {
        public List<Guid> EnqueuedTokenIds { get; } = [];

        public Task EnqueueAsync(Guid tokenId, CancellationToken cancellationToken = default)
        {
            EnqueuedTokenIds.Add(tokenId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserRepository(User? user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(user?.SecurityStamp);

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(user is not null && user.NormalizedEmail == normalizedEmail ? user : null);

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(user is not null && user.NormalizedEmail == normalizedEmail);

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeEmailVerificationTokenRepository : IEmailVerificationTokenRepository
    {
        public List<EmailVerificationToken> CreatedTokens { get; } = [];

        public int InvalidationCount { get; private set; }

        public Task<EmailVerificationTokenConsumptionResult?> TryConsumeActiveTokenAsync(
            string tokenHash,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EmailVerificationTokenConsumptionResult?>(null);

        public Task CreateAsync(EmailVerificationToken token, CancellationToken cancellationToken = default)
        {
            CreatedTokens.Add(token);
            return Task.CompletedTask;
        }

        public Task InvalidateActiveTokensForUserAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            InvalidationCount++;
            return Task.CompletedTask;
        }

        public Task<EmailVerificationDeliveryTarget?> GetDeliveryTargetAsync(
            Guid tokenId,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EmailVerificationDeliveryTarget?>(null);

        public Task CompleteDeliveryAsync(
            Guid tokenId,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
