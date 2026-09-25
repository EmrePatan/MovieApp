using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;

namespace MovieApp.UnitTests.Identity;

public sealed class PasswordResetDeliveryTests
{
    [Fact]
    public async Task ForgotPasswordAsyncEnqueuesDeliveryWithoutSendingEmailInline()
    {
        var emailSender = new RecordingPasswordResetEmailSender();
        var enqueuer = new RecordingEnqueuer();
        var service = CreateForgotPasswordService(emailSender, enqueuer);

        await service.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com"));

        Assert.Single(enqueuer.EnqueuedTokenIds);
        Assert.Empty(emailSender.SentPasswordResetEmails);
    }

    [Fact]
    public async Task ForgotPasswordAsyncCreatesProtectedDeliverySecretAndContentLocale()
    {
        var tokenRepository = new TrackingPasswordResetTokenRepository();
        var service = CreateForgotPasswordService(
            new RecordingPasswordResetEmailSender(),
            new RecordingEnqueuer(),
            tokenRepository);

        await service.ForgotPasswordAsync(new ForgotPasswordRequest(
            "user@example.com",
            ContentLocaleResolver.TurkishTurkey));

        Assert.Single(tokenRepository.CreatedTokens);
        Assert.NotNull(tokenRepository.CreatedTokens[0].ProtectedDeliverySecret);
        Assert.Equal(ContentLocaleResolver.TurkishTurkey, tokenRepository.CreatedTokens[0].ContentLocale);
    }

    [Fact]
    public async Task DeliverAsyncRetryDoesNotCreateOrInvalidateTokens()
    {
        var tokenRepository = new TrackingPasswordResetTokenRepository();
        var emailSender = new RecordingPasswordResetEmailSender { FailCount = 1 };
        var service = CreateDeliveryService(tokenRepository, emailSender);
        var tokenId = await SeedDeliverableTokenAsync(tokenRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeliverAsync(tokenId));
        await service.DeliverAsync(tokenId);

        Assert.Single(tokenRepository.CreatedTokens);
        Assert.Equal(0, tokenRepository.InvalidationCount);
        Assert.Single(emailSender.SentPasswordResetEmails);
        Assert.Contains(tokenId, tokenRepository.CompletedTokenIds);
    }

    [Fact]
    public async Task DeliverAsyncKeepsProtectedSecretWhenProviderFails()
    {
        var tokenRepository = new TrackingPasswordResetTokenRepository();
        var emailSender = new RecordingPasswordResetEmailSender { FailCount = 2 };
        var service = CreateDeliveryService(tokenRepository, emailSender);
        var tokenId = await SeedDeliverableTokenAsync(tokenRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeliverAsync(tokenId));

        var token = tokenRepository.CreatedTokens.Single(item => item.Id == tokenId);
        Assert.NotNull(token.ProtectedDeliverySecret);
        Assert.DoesNotContain(tokenId, tokenRepository.CompletedTokenIds);
    }

    [Fact]
    public async Task DeliverAsyncClearsProtectedSecretAfterSuccessfulDelivery()
    {
        var tokenRepository = new TrackingPasswordResetTokenRepository();
        var emailSender = new RecordingPasswordResetEmailSender();
        var service = CreateDeliveryService(tokenRepository, emailSender);
        var tokenId = await SeedDeliverableTokenAsync(tokenRepository);

        await service.DeliverAsync(tokenId);

        var token = tokenRepository.CreatedTokens.Single(item => item.Id == tokenId);
        Assert.Null(token.ProtectedDeliverySecret);
        Assert.Contains(tokenId, tokenRepository.CompletedTokenIds);
    }

    [Fact]
    public async Task DeliverAsyncUsesPersistedContentLocaleFromToken()
    {
        var tokenRepository = new TrackingPasswordResetTokenRepository();
        var emailSender = new RecordingPasswordResetEmailSender();
        var service = CreateDeliveryService(tokenRepository, emailSender);
        var rawToken = PasswordResetTokenGenerator.GenerateToken();
        var token = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = PasswordResetTokenHasher.HashToken(rawToken),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            ProtectedDeliverySecret = rawToken,
            ContentLocale = ContentLocaleResolver.TurkishTurkey
        };

        await tokenRepository.CreateAsync(token);

        await service.DeliverAsync(token.Id);

        Assert.Equal(
            ContentLocaleResolver.TurkishTurkey,
            emailSender.SentPasswordResetEmails.Single().ContentLocale);
    }

    [Theory]
    [InlineData("tr")]
    [InlineData("tr-TR")]
    [InlineData("tr;q=0.9,en-US;q=0.8")]
    public async Task ForgotPasswordAsyncNormalizesTurkishContentLocale(string contentLocale)
    {
        var tokenRepository = new TrackingPasswordResetTokenRepository();
        var service = CreateForgotPasswordService(
            new RecordingPasswordResetEmailSender(),
            new RecordingEnqueuer(),
            tokenRepository);

        await service.ForgotPasswordAsync(new ForgotPasswordRequest("user@example.com", contentLocale));

        Assert.Equal(ContentLocaleResolver.TurkishTurkey, tokenRepository.CreatedTokens[0].ContentLocale);
    }

    [Fact]
    public async Task DeliverAsyncBuildsResetUrlWithRawToken()
    {
        var tokenRepository = new TrackingPasswordResetTokenRepository();
        var emailSender = new RecordingPasswordResetEmailSender();
        var service = CreateDeliveryService(tokenRepository, emailSender);
        var rawToken = PasswordResetTokenGenerator.GenerateToken();
        var tokenId = Guid.NewGuid();
        var token = new PasswordResetToken
        {
            Id = tokenId,
            UserId = Guid.NewGuid(),
            TokenHash = PasswordResetTokenHasher.HashToken(rawToken),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            ProtectedDeliverySecret = rawToken
        };

        await tokenRepository.CreateAsync(token);

        await service.DeliverAsync(tokenId);

        var sent = emailSender.SentPasswordResetEmails.Single();
        Assert.Equal(
            ForgotPasswordService.BuildResetUrl("movieapp://reset-password", rawToken),
            sent.ResetUrl);
        Assert.Equal(rawToken, emailSender.LastRawToken);
    }

    [Fact]
    public async Task ResetPasswordSucceedsAfterDelayedDelivery()
    {
        var tokenRepository = new TrackingPasswordResetTokenRepository();
        var emailSender = new RecordingPasswordResetEmailSender();
        var enqueuer = new RecordingEnqueuer();
        var forgotPasswordService = CreateForgotPasswordService(emailSender, enqueuer, tokenRepository);
        var user = CreateUser();

        await forgotPasswordService.ForgotPasswordAsync(new ForgotPasswordRequest(user.Email));

        var tokenId = enqueuer.EnqueuedTokenIds.Single();
        var deliveryService = CreateDeliveryService(tokenRepository, emailSender);
        await deliveryService.DeliverAsync(tokenId);

        var rawToken = emailSender.LastRawToken!;
        var resetService = new ResetPasswordService(
            new TransactionalApplicationDbContext(),
            new FakeUserRepository(user),
            tokenRepository,
            new FakePasswordHasher(),
            new FakeAuthenticationSessionService());

        var result = await resetService.ResetPasswordAsync(new ResetPasswordRequest(rawToken, "NewPassword1!"));

        Assert.Equal(ResetPasswordService.SuccessMessage, result.Message);
    }

    private static ForgotPasswordService CreateForgotPasswordService(
        RecordingPasswordResetEmailSender emailSender,
        IPasswordResetDeliveryEnqueuer enqueuer,
        TrackingPasswordResetTokenRepository? tokenRepository = null,
        User? user = null) =>
        new(
            new FakeUserRepository(user ?? CreateUser()),
            tokenRepository ?? new TrackingPasswordResetTokenRepository(),
            new PassthroughProtector(),
            enqueuer,
            Options.Create(new PasswordResetOptions
            {
                TokenLifetimeMinutes = 60,
                BaseUrl = "movieapp://reset-password"
            }),
            NullLogger<ForgotPasswordService>.Instance);

    private static PasswordResetDeliveryService CreateDeliveryService(
        TrackingPasswordResetTokenRepository tokenRepository,
        RecordingPasswordResetEmailSender emailSender) =>
        new(
            tokenRepository,
            new PassthroughProtector(),
            emailSender,
            Options.Create(new PasswordResetOptions
            {
                TokenLifetimeMinutes = 60,
                BaseUrl = "movieapp://reset-password"
            }),
            NullLogger<PasswordResetDeliveryService>.Instance);

    private static async Task<Guid> SeedDeliverableTokenAsync(
        TrackingPasswordResetTokenRepository tokenRepository)
    {
        var rawToken = PasswordResetTokenGenerator.GenerateToken();
        var token = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = PasswordResetTokenHasher.HashToken(rawToken),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            ProtectedDeliverySecret = rawToken
        };

        await tokenRepository.CreateAsync(token);
        tokenRepository.SeedDeliveryTarget(new PasswordResetDeliveryTarget(
            token.Id,
            token.UserId,
            "user@example.com",
            rawToken,
            true,
            ContentLocaleResolver.EnglishUnitedStates));

        return token.Id;
    }

    private static User CreateUser() =>
        User.Create(
            Guid.NewGuid(),
            "user@example.com",
            "hashed-password",
            "Display Name",
            DateTime.UtcNow);

    private sealed class PassthroughProtector : IPasswordResetDeliverySecretProtector
    {
        public string Protect(string rawToken) => rawToken;

        public string Unprotect(string protectedPayload) => protectedPayload;
    }

    private sealed class RecordingEnqueuer : IPasswordResetDeliveryEnqueuer
    {
        public List<Guid> EnqueuedTokenIds { get; } = [];

        public Task EnqueueAsync(Guid tokenId, CancellationToken cancellationToken = default)
        {
            EnqueuedTokenIds.Add(tokenId);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPasswordResetEmailSender : IPasswordResetEmailSender
    {
        public int FailCount { get; init; }

        public List<(Guid TokenId, string Email, string ResetUrl, string ContentLocale)> SentPasswordResetEmails { get; } = [];

        public string? LastRawToken { get; private set; }

        private int _attempts;

        public Task SendPasswordResetEmailAsync(
            Guid tokenId,
            string toEmail,
            string resetUrl,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            _attempts++;
            if (_attempts <= FailCount)
            {
                throw new InvalidOperationException("delivery failed");
            }

            SentPasswordResetEmails.Add((tokenId, toEmail, resetUrl, contentLocale));
            LastRawToken = ExtractToken(resetUrl);
            return Task.CompletedTask;
        }

        private static string? ExtractToken(string resetUrl)
        {
            var queryIndex = resetUrl.IndexOf('?', StringComparison.Ordinal);
            if (queryIndex < 0)
            {
                return null;
            }

            foreach (var part in resetUrl[(queryIndex + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                if (pair.Length == 2 &&
                    string.Equals(pair[0], "token", StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(pair[1]);
                }
            }

            return null;
        }
    }

    private sealed class TrackingPasswordResetTokenRepository : IPasswordResetTokenRepository
    {
        private readonly Dictionary<Guid, PasswordResetDeliveryTarget> _deliveryTargets = new();

        public List<PasswordResetToken> CreatedTokens { get; } = [];

        public List<Guid> CompletedTokenIds { get; } = [];

        public int InvalidationCount { get; private set; }

        public void SeedDeliveryTarget(PasswordResetDeliveryTarget target) =>
            _deliveryTargets[target.TokenId] = target;

        public Task CreateAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
        {
            CreatedTokens.Add(token);
            _deliveryTargets[token.Id] = new PasswordResetDeliveryTarget(
                token.Id,
                token.UserId,
                "user@example.com",
                token.ProtectedDeliverySecret ?? string.Empty,
                token.ProtectedDeliverySecret is not null,
                token.ContentLocale);
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

        public Task<PasswordResetToken?> GetActiveByTokenHashAsync(
            string tokenHash,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PasswordResetToken?>(null);

        public Task<PasswordResetTokenConsumptionResult?> TryConsumeActiveTokenAsync(
            string tokenHash,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var token = CreatedTokens.FirstOrDefault(item =>
                item.TokenHash == tokenHash &&
                item.UsedAtUtc is null &&
                item.ExpiresAtUtc > utcNow);

            if (token is null)
            {
                return Task.FromResult<PasswordResetTokenConsumptionResult?>(null);
            }

            token.UsedAtUtc = utcNow;
            return Task.FromResult<PasswordResetTokenConsumptionResult?>(
                new PasswordResetTokenConsumptionResult(token.Id, token.UserId));
        }

        public Task<PasswordResetDeliveryTarget?> GetDeliveryTargetAsync(
            Guid tokenId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            if (!_deliveryTargets.TryGetValue(tokenId, out var target))
            {
                return Task.FromResult<PasswordResetDeliveryTarget?>(null);
            }

            var token = CreatedTokens.Single(item => item.Id == tokenId);
            if (CompletedTokenIds.Contains(tokenId))
            {
                return Task.FromResult<PasswordResetDeliveryTarget?>(
                    target with { IsDeliverable = false });
            }

            return Task.FromResult<PasswordResetDeliveryTarget?>(target);
        }

        public Task CompleteDeliveryAsync(
            Guid tokenId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            CompletedTokenIds.Add(tokenId);
            var token = CreatedTokens.Single(item => item.Id == tokenId);
            token.ProtectedDeliverySecret = null;
            token.DeliveryCompletedAtUtc = utcNow;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(user);

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(user);

        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(user.SecurityStamp);

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(user.NormalizedEmail == normalizedEmail ? user : null);

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(user.NormalizedEmail == normalizedEmail);

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => $"hashed:{password}";

        public bool VerifyPassword(string password, string passwordHash) =>
            passwordHash == HashPassword(password);
    }

    private sealed class TransactionalApplicationDbContext : IApplicationDbContext
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default) =>
            action(cancellationToken);
    }
}
