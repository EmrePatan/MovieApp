using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;
using MovieApp.UnitTests.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MovieApp.UnitTests.Identity;

public sealed class PendingEmailChangeIntentTests
{
    [Fact]
    public async Task ProfilePendingEmailExistsWhileTokenActive()
    {
        var user = CreateVerifiedPasswordUser();
        var tokenRepository = new IntentTrackingTokenRepository();
        tokenRepository.SeedPendingToken(user.Id, "new@example.com", DateTime.UtcNow.AddHours(1));

        var profile = await CreateProfileService(user, tokenRepository).GetCurrentProfileAsync();

        Assert.Equal("new@example.com", profile.PendingEmail);
    }

    [Fact]
    public async Task ProfilePendingEmailStillExistsAfterTokenExpires()
    {
        var user = CreateVerifiedPasswordUser();
        var tokenRepository = new IntentTrackingTokenRepository();
        tokenRepository.SeedPendingToken(user.Id, "new@example.com", DateTime.UtcNow.AddHours(-1));

        var profile = await CreateProfileService(user, tokenRepository).GetCurrentProfileAsync();

        Assert.Equal("new@example.com", profile.PendingEmail);
    }

    [Fact]
    public async Task ResendAfterExpiryCreatesFreshTokenForSamePendingEmail()
    {
        var user = CreateVerifiedPasswordUser();
        var tokenRepository = new IntentTrackingTokenRepository();
        var expired = tokenRepository.SeedPendingToken(user.Id, "new@example.com", DateTime.UtcNow.AddHours(-1));
        var service = CreateCredentialService(user, tokenRepository);

        await service.ResendPendingEmailChangeAsync("en-US");

        Assert.Equal(2, tokenRepository.CreatedTokens.Count);
        Assert.NotNull(expired.UsedAtUtc);
        Assert.Equal("new@example.com", tokenRepository.CreatedTokens[^1].PendingEmail);
        Assert.True(tokenRepository.CreatedTokens[^1].ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task ExpiredTokenCannotBeConsumed()
    {
        var user = CreateVerifiedPasswordUser();
        var tokenRepository = new IntentTrackingTokenRepository();
        var expired = tokenRepository.SeedPendingToken(user.Id, "new@example.com", DateTime.UtcNow.AddHours(-1), "raw-expired");
        var verifyService = CreateVerifyService(user, tokenRepository);

        await Assert.ThrowsAsync<ValidationException>(() =>
            verifyService.VerifyEmailAsync(new VerifyEmailRequest("raw-expired")));

        Assert.Null(expired.UsedAtUtc);
        Assert.Equal("user@example.com", user.Email);
    }

    [Fact]
    public async Task NewerPendingEmailReplacesOlderIntent()
    {
        var user = CreateVerifiedPasswordUser();
        var tokenRepository = new IntentTrackingTokenRepository();
        var older = tokenRepository.SeedPendingToken(user.Id, "older@example.com", DateTime.UtcNow.AddHours(-1), "older-token");
        var service = CreateCredentialService(user, tokenRepository);

        await service.RequestEmailChangeAsync(
            new RequestEmailChangeCommand(
                "newer@example.com",
                new AccountReauthenticationProof("StrongPassword123", null, null),
                "en-US"));

        Assert.NotNull(older.UsedAtUtc);
        var profile = await CreateProfileService(user, tokenRepository).GetCurrentProfileAsync();
        Assert.Equal("newer@example.com", profile.PendingEmail);
    }

    [Fact]
    public async Task OldExpiredLinkCannotApplyAfterNewerRequest()
    {
        var user = CreateVerifiedPasswordUser();
        var tokenRepository = new IntentTrackingTokenRepository();
        tokenRepository.SeedPendingToken(user.Id, "older@example.com", DateTime.UtcNow.AddHours(-1), "older-token");
        var service = CreateCredentialService(user, tokenRepository);
        await service.RequestEmailChangeAsync(
            new RequestEmailChangeCommand(
                "newer@example.com",
                new AccountReauthenticationProof("StrongPassword123", null, null),
                "en-US"));

        var verifyService = CreateVerifyService(user, tokenRepository);
        await Assert.ThrowsAsync<ValidationException>(() =>
            verifyService.VerifyEmailAsync(new VerifyEmailRequest("older-token")));
    }

    [Fact]
    public async Task SuccessfulVerificationClearsPendingState()
    {
        var user = CreateVerifiedPasswordUser();
        var tokenRepository = new IntentTrackingTokenRepository();
        tokenRepository.SeedPendingToken(user.Id, "new@example.com", DateTime.UtcNow.AddHours(1), "fresh-token");
        var verifyService = CreateVerifyService(user, tokenRepository);

        await verifyService.VerifyEmailAsync(new VerifyEmailRequest("fresh-token"));

        var profile = await CreateProfileService(user, tokenRepository).GetCurrentProfileAsync();
        Assert.Equal("new@example.com", user.Email);
        Assert.Null(profile.PendingEmail);
    }

    [Fact]
    public async Task ResendAfterSuccessfulVerificationReturnsNoPendingEmailChange()
    {
        var user = CreateVerifiedPasswordUser();
        var tokenRepository = new IntentTrackingTokenRepository();
        tokenRepository.SeedPendingToken(user.Id, "new@example.com", DateTime.UtcNow.AddHours(1), "fresh-token");
        await CreateVerifyService(user, tokenRepository).VerifyEmailAsync(new VerifyEmailRequest("fresh-token"));

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateCredentialService(user, tokenRepository).ResendPendingEmailChangeAsync("en-US"));

        Assert.Equal(AccountAuthenticationErrorCodes.NoPendingEmailChange, exception.ErrorCode);
    }

    private static User CreateVerifiedPasswordUser()
    {
        var user = User.Create(Guid.NewGuid(), "user@example.com", "hash", "User", DateTime.UtcNow.AddDays(-1));
        user.MarkEmailVerified(DateTime.UtcNow.AddDays(-1));
        return user;
    }

    private static UserProfileService CreateProfileService(User user, IntentTrackingTokenRepository tokenRepository) =>
        new(
            new FakeCurrentUser(user.Id),
            new FakeUserRepository(user),
            new FakeExternalLoginRepository(),
            tokenRepository,
            new FakeStatisticsRepository(),
            new FakeProfileStatisticsCache(),
            new FakePasswordHasher(),
            new FakeAuthenticationSessionService(),
            new AccountReauthenticationService(
                new FakeExternalLoginRepository(),
                new FakePasswordHasher(),
                []));

    private static UserCredentialMethodsService CreateCredentialService(
        User user,
        IntentTrackingTokenRepository tokenRepository) =>
        new(
            new FakeCurrentUser(user.Id),
            new FakeUserRepository(user),
            new FakeExternalLoginRepository(),
            new AccountReauthenticationService(
                new FakeExternalLoginRepository(),
                new FakePasswordHasher(),
                []),
            new FakePasswordHasher(),
            new FakeAuthenticationSessionService(),
            tokenRepository,
            new FakeDeliverySecretProtector(),
            new FakeDeliveryEnqueuer(),
            Options.Create(new EmailVerificationOptions { TokenLifetimeMinutes = 60 }),
            NullLogger<UserCredentialMethodsService>.Instance);

    private static VerifyEmailService CreateVerifyService(User user, IntentTrackingTokenRepository tokenRepository) =>
        new(
            new FakeApplicationDbContext(),
            new FakeUserRepository(user),
            tokenRepository,
            new FakeAuthenticationSessionService(),
            new FakeAccountEmailChangedNotificationSender(),
            NullLogger<VerifyEmailService>.Instance);

    private sealed class IntentTrackingTokenRepository : IEmailVerificationTokenRepository
    {
        public List<EmailVerificationToken> CreatedTokens { get; } = [];

        public EmailVerificationToken SeedPendingToken(
            Guid userId,
            string pendingEmail,
            DateTime expiresAtUtc,
            string rawToken = "token")
        {
            var token = new EmailVerificationToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = PasswordResetTokenHasher.HashToken(rawToken),
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = expiresAtUtc,
                PendingEmail = pendingEmail,
                ContentLocale = "en-US",
            };
            CreatedTokens.Add(token);
            return token;
        }

        public Task<EmailVerificationTokenConsumptionResult?> TryConsumeActiveTokenAsync(
            string tokenHash,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var token = CreatedTokens.FirstOrDefault(item => item.TokenHash == tokenHash);
            if (token is null || token.UsedAtUtc is not null || token.ExpiresAtUtc <= utcNow)
            {
                return Task.FromResult<EmailVerificationTokenConsumptionResult?>(null);
            }

            token.UsedAtUtc = utcNow;
            return Task.FromResult<EmailVerificationTokenConsumptionResult?>(
                new EmailVerificationTokenConsumptionResult(
                    token.Id,
                    token.UserId,
                    token.PendingEmail,
                    token.ContentLocale));
        }

        public Task CreateAsync(EmailVerificationToken token, CancellationToken cancellationToken = default)
        {
            CreatedTokens.Add(token);
            return Task.CompletedTask;
        }

        public Task InvalidateActiveTokensForUserAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

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

        public Task<string?> GetPendingEmailChangeIntentForUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var pending = CreatedTokens
                .Where(token => token.UserId == userId && token.UsedAtUtc is null && !string.IsNullOrWhiteSpace(token.PendingEmail))
                .OrderByDescending(token => token.CreatedAtUtc)
                .Select(token => token.PendingEmail)
                .FirstOrDefault();
            return Task.FromResult(pending);
        }

        public Task InvalidateUnusedPendingEmailChangeTokensForUserAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            foreach (var token in CreatedTokens.Where(token =>
                         token.UserId == userId &&
                         token.UsedAtUtc is null &&
                         !string.IsNullOrWhiteSpace(token.PendingEmail)))
            {
                token.UsedAtUtc = utcNow;
            }

            return Task.CompletedTask;
        }
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
            Task.FromResult<User?>(user.Id == id ? user : null);

        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(user.SecurityStamp);

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeExternalLoginRepository : IUserExternalLoginRepository
    {
        public Task<User?> GetUserByProviderAndSubjectAsync(
            string provider,
            string providerSubject,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<UserExternalLogin> CreateAsync(
            UserExternalLogin externalLogin,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(externalLogin);

        public Task<IReadOnlyList<string>> GetProvidersForUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<bool> DeleteAsync(Guid userId, string provider, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeStatisticsRepository : IUserStatisticsRepository
    {
        public Task<UserStatisticsResult> GetStatisticsAsync(
            Guid userId,
            string? timeZoneId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => password;
        public bool VerifyPassword(string password, string passwordHash) => password == "StrongPassword123";
    }

    private sealed class FakeDeliverySecretProtector : IEmailVerificationDeliverySecretProtector
    {
        public string Protect(string rawToken) => rawToken;
        public string Unprotect(string protectedSecret) => protectedSecret;
    }

    private sealed class FakeDeliveryEnqueuer : IEmailVerificationDeliveryEnqueuer
    {
        public Task EnqueueAsync(Guid tokenId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeApplicationDbContext : IApplicationDbContext
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default) =>
            action(cancellationToken);
    }

    private sealed class FakeAccountEmailChangedNotificationSender : IAccountEmailChangedNotificationSender
    {
        public Task SendAsync(
            string previousEmail,
            string newEmail,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
