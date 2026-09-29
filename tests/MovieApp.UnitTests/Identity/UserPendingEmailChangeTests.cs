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

public sealed class UserPendingEmailChangeTests
{
    [Fact]
    public async Task RequestEmailChangeStoresPendingEmailOnUser()
    {
        var user = CreateVerifiedPasswordUser();
        var service = CreateCredentialService(user, new TrackingTokenRepository());

        await service.RequestEmailChangeAsync(
            new RequestEmailChangeCommand(
                "new@example.com",
                new AccountReauthenticationProof("StrongPassword123", null, null),
                "en-US"));

        Assert.Equal("new@example.com", user.PendingEmail);
        Assert.Equal("user@example.com", user.Email);
    }

    [Fact]
    public async Task ExpiredTokenDoesNotClearUserPendingEmail()
    {
        var user = CreateVerifiedPasswordUser();
        user.SetPendingEmailChange("new@example.com", DateTime.UtcNow);
        var tokenRepository = new TrackingTokenRepository();
        tokenRepository.SeedActiveToken(user.Id, "new@example.com", DateTime.UtcNow.AddHours(-1), "expired");

        var profile = await CreateProfileService(user, tokenRepository).GetCurrentProfileAsync();

        Assert.Equal("new@example.com", profile.PendingEmail);
        Assert.Equal("new@example.com", user.PendingEmail);
    }

    [Fact]
    public async Task ResendUsesUserPendingEmail()
    {
        var user = CreateVerifiedPasswordUser();
        user.SetPendingEmailChange("new@example.com", DateTime.UtcNow);
        var tokenRepository = new TrackingTokenRepository();
        var service = CreateCredentialService(user, tokenRepository);

        await service.ResendPendingEmailChangeAsync("en-US");

        Assert.Equal("new@example.com", tokenRepository.CreatedTokens[^1].PendingEmail);
    }

    [Fact]
    public async Task ResendCreatesFreshTokenAndOldTokenRemainsUnusable()
    {
        var user = CreateVerifiedPasswordUser();
        user.SetPendingEmailChange("new@example.com", DateTime.UtcNow);
        var tokenRepository = new TrackingTokenRepository();
        var expired = tokenRepository.SeedActiveToken(user.Id, "new@example.com", DateTime.UtcNow.AddHours(-1), "old-token");
        var service = CreateCredentialService(user, tokenRepository);

        await service.ResendPendingEmailChangeAsync("en-US");

        Assert.NotNull(expired.UsedAtUtc);
        Assert.Equal(2, tokenRepository.CreatedTokens.Count);
        Assert.True(tokenRepository.CreatedTokens[^1].ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task NewerRequestReplacesUserPendingEmail()
    {
        var user = CreateVerifiedPasswordUser();
        var service = CreateCredentialService(user, new TrackingTokenRepository());

        await service.RequestEmailChangeAsync(
            new RequestEmailChangeCommand(
                "older@example.com",
                new AccountReauthenticationProof("StrongPassword123", null, null),
                "en-US"));
        await service.RequestEmailChangeAsync(
            new RequestEmailChangeCommand(
                "newer@example.com",
                new AccountReauthenticationProof("StrongPassword123", null, null),
                "en-US"));

        Assert.Equal("newer@example.com", user.PendingEmail);
    }

    [Fact]
    public async Task OldTokenCannotVerifyAfterNewerRequest()
    {
        var user = CreateVerifiedPasswordUser();
        var tokenRepository = new TrackingTokenRepository();
        tokenRepository.SeedActiveToken(user.Id, "older@example.com", DateTime.UtcNow.AddHours(1), "older-token");
        var service = CreateCredentialService(user, tokenRepository);

        await service.RequestEmailChangeAsync(
            new RequestEmailChangeCommand(
                "newer@example.com",
                new AccountReauthenticationProof("StrongPassword123", null, null),
                "en-US"));

        var verifyService = CreateVerifyService(user, tokenRepository);
        await Assert.ThrowsAsync<ValidationException>(() =>
            verifyService.VerifyEmailAsync(new VerifyEmailRequest("older-token")));
        Assert.Equal("user@example.com", user.Email);
    }

    [Fact]
    public async Task SuccessfulVerifyMovesPendingEmailToEmailAndClearsPending()
    {
        var user = CreateVerifiedPasswordUser();
        user.SetPendingEmailChange("new@example.com", DateTime.UtcNow);
        var tokenRepository = new TrackingTokenRepository();
        tokenRepository.SeedActiveToken(user.Id, "new@example.com", DateTime.UtcNow.AddHours(1), "fresh-token");
        var verifyService = CreateVerifyService(user, tokenRepository);

        await verifyService.VerifyEmailAsync(new VerifyEmailRequest("fresh-token"));

        Assert.Equal("new@example.com", user.Email);
        Assert.Null(user.PendingEmail);
        var profile = await CreateProfileService(user, tokenRepository).GetCurrentProfileAsync();
        Assert.Null(profile.PendingEmail);
    }

    [Fact]
    public async Task ResendAfterSuccessfulVerifyReturnsNoPendingEmailChange()
    {
        var user = CreateVerifiedPasswordUser();
        user.SetPendingEmailChange("new@example.com", DateTime.UtcNow);
        var tokenRepository = new TrackingTokenRepository();
        tokenRepository.SeedActiveToken(user.Id, "new@example.com", DateTime.UtcNow.AddHours(1), "fresh-token");
        await CreateVerifyService(user, tokenRepository).VerifyEmailAsync(new VerifyEmailRequest("fresh-token"));

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateCredentialService(user, tokenRepository).ResendPendingEmailChangeAsync("en-US"));

        Assert.Equal(AccountAuthenticationErrorCodes.NoPendingEmailChange, exception.ErrorCode);
    }

    [Fact]
    public async Task HistoricalTokenWithPendingEmailCannotChangeEmailWhenUserPendingIsNull()
    {
        var user = CreateVerifiedPasswordUser();
        var tokenRepository = new TrackingTokenRepository();
        tokenRepository.SeedActiveToken(user.Id, "new@example.com", DateTime.UtcNow.AddHours(1), "stale-token");
        var verifyService = CreateVerifyService(user, tokenRepository);

        await Assert.ThrowsAsync<ValidationException>(() =>
            verifyService.VerifyEmailAsync(new VerifyEmailRequest("stale-token")));

        Assert.Equal("user@example.com", user.Email);
        Assert.Null(user.PendingEmail);
    }

    [Fact]
    public async Task TokenPendingEmailMismatchingUserPendingCannotChangeEmail()
    {
        var user = CreateVerifiedPasswordUser();
        user.SetPendingEmailChange("newer@example.com", DateTime.UtcNow);
        var tokenRepository = new TrackingTokenRepository();
        tokenRepository.SeedActiveToken(user.Id, "older@example.com", DateTime.UtcNow.AddHours(1), "mismatch-token");
        var verifyService = CreateVerifyService(user, tokenRepository);

        await Assert.ThrowsAsync<ValidationException>(() =>
            verifyService.VerifyEmailAsync(new VerifyEmailRequest("mismatch-token")));

        Assert.Equal("user@example.com", user.Email);
        Assert.Equal("newer@example.com", user.PendingEmail);
    }

    [Fact]
    public async Task SerializedReplacementRequestsLeaveOnlyLatestPendingEmail()
    {
        var user = CreateVerifiedPasswordUser();
        var service = CreateCredentialService(user, new TrackingTokenRepository());

        await service.RequestEmailChangeAsync(
            new RequestEmailChangeCommand(
                "first@example.com",
                new AccountReauthenticationProof("StrongPassword123", null, null),
                "en-US"));
        await service.RequestEmailChangeAsync(
            new RequestEmailChangeCommand(
                "second@example.com",
                new AccountReauthenticationProof("StrongPassword123", null, null),
                "en-US"));
        await service.RequestEmailChangeAsync(
            new RequestEmailChangeCommand(
                "third@example.com",
                new AccountReauthenticationProof("StrongPassword123", null, null),
                "en-US"));

        Assert.Equal("third@example.com", user.PendingEmail);
        var profile = await CreateProfileService(user, new TrackingTokenRepository()).GetCurrentProfileAsync();
        Assert.Equal("third@example.com", profile.PendingEmail);
    }

    [Fact]
    public async Task VerifyRejectsDuplicateEmailBeforeApplyingChange()
    {
        var user = CreateVerifiedPasswordUser();
        user.SetPendingEmailChange("taken@example.com", DateTime.UtcNow);
        var tokenRepository = new TrackingTokenRepository();
        tokenRepository.SeedActiveToken(user.Id, "taken@example.com", DateTime.UtcNow.AddHours(1), "dup-token");
        var verifyService = CreateVerifyService(
            user,
            tokenRepository,
            existingNormalizedEmail: UserEmailNormalizer.Normalize("taken@example.com"));

        await Assert.ThrowsAsync<ValidationException>(() =>
            verifyService.VerifyEmailAsync(new VerifyEmailRequest("dup-token")));

        Assert.Equal("user@example.com", user.Email);
        Assert.Equal("taken@example.com", user.PendingEmail);
    }

    private static User CreateVerifiedPasswordUser()
    {
        var user = User.Create(Guid.NewGuid(), "user@example.com", "hash", "User", DateTime.UtcNow.AddDays(-1));
        user.MarkEmailVerified(DateTime.UtcNow.AddDays(-1));
        return user;
    }

    private static UserProfileService CreateProfileService(User user, TrackingTokenRepository tokenRepository) =>
        new(
            new FakeCurrentUser(user.Id),
            new FakeUserRepository(user),
            new FakeExternalLoginRepository(),
            new FakeStatisticsRepository(),
            new FakeProfileStatisticsCache(),
            new FakePasswordHasher(),
            new FakeAuthenticationSessionService(),
            new AccountReauthenticationService(new FakeExternalLoginRepository(), new FakePasswordHasher(), []));

    private static UserCredentialMethodsService CreateCredentialService(
        User user,
        TrackingTokenRepository tokenRepository) =>
        new(
            new FakeCurrentUser(user.Id),
            new FakeApplicationDbContext(),
            new FakeUserRepository(user),
            new FakeExternalLoginRepository(),
            new AccountReauthenticationService(new FakeExternalLoginRepository(), new FakePasswordHasher(), []),
            new FakePasswordHasher(),
            new FakeAuthenticationSessionService(),
            tokenRepository,
            new FakeDeliverySecretProtector(),
            new FakeDeliveryEnqueuer(),
            Options.Create(new EmailVerificationOptions { TokenLifetimeMinutes = 60 }),
            NullLogger<UserCredentialMethodsService>.Instance);

    private static VerifyEmailService CreateVerifyService(
        User user,
        TrackingTokenRepository tokenRepository,
        string? existingNormalizedEmail = null) =>
        new(
            new FakeApplicationDbContext(),
            new FakeUserRepository(user, existingNormalizedEmail),
            tokenRepository,
            new FakeAuthenticationSessionService(),
            new FakeAccountEmailChangedNotificationSender(),
            NullLogger<VerifyEmailService>.Instance);

    private sealed class TrackingTokenRepository : IEmailVerificationTokenRepository
    {
        public List<EmailVerificationToken> CreatedTokens { get; } = [];

        public EmailVerificationToken SeedActiveToken(
            Guid userId,
            string pendingEmail,
            DateTime expiresAtUtc,
            string rawToken)
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
            var active = GetActiveTokenByHashAsync(tokenHash, utcNow, cancellationToken).Result;
            if (active is null || !TryMarkTokenUsedAsync(active.TokenId, utcNow, cancellationToken).Result)
            {
                return Task.FromResult<EmailVerificationTokenConsumptionResult?>(null);
            }

            return Task.FromResult<EmailVerificationTokenConsumptionResult?>(active);
        }

        public Task<EmailVerificationTokenConsumptionResult?> GetActiveTokenByHashAsync(
            string tokenHash,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var token = CreatedTokens.FirstOrDefault(item =>
                item.TokenHash == tokenHash &&
                item.UsedAtUtc is null &&
                item.ExpiresAtUtc > utcNow);

            return Task.FromResult(token is null
                ? null
                : new EmailVerificationTokenConsumptionResult(
                    token.Id,
                    token.UserId,
                    token.PendingEmail,
                    token.ContentLocale));
        }

        public Task<bool> TryMarkTokenUsedAsync(
            Guid tokenId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var token = CreatedTokens.FirstOrDefault(item => item.Id == tokenId);
            if (token is null || token.UsedAtUtc is not null || token.ExpiresAtUtc <= utcNow)
            {
                return Task.FromResult(false);
            }

            token.UsedAtUtc = utcNow;
            return Task.FromResult(true);
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

    private sealed class FakeUserRepository(User user, string? existingNormalizedEmail = null) : IUserRepository
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
            Task.FromResult(
                existingNormalizedEmail is not null &&
                existingNormalizedEmail == normalizedEmail &&
                user.NormalizedEmail != normalizedEmail);

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

    private sealed class FakeApplicationDbContext : IApplicationDbContext
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default) =>
            action(cancellationToken);
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
