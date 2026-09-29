using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MovieApp.UnitTests.Identity;

public sealed class UserCredentialMethodsServiceTests
{
    [Fact]
    public async Task LinkExternalLoginAsyncCreatesProviderWhenBothSidesAuthenticated()
    {
        var user = CreatePasswordUser();
        var externalLoginRepository = new CredentialFakeExternalLoginRepository();
        var targetVerifier = new CredentialFakeSocialVerifier(
            ExternalLoginProviders.Apple,
            new VerifiedSocialIdentity(
                ExternalLoginProviders.Apple,
                "apple-target-subject",
                "other@example.com",
                true,
                "Apple"));

        var service = CreateService(user, externalLoginRepository, targetVerifier);

        var profile = await service.LinkExternalLoginAsync(
            new LinkExternalLoginCommand(
                new AccountReauthenticationProof("StrongPassword123", null, null),
                ExternalLoginProviders.Apple,
                "target-token"));

        Assert.Contains(ExternalLoginProviders.Apple, profile.LinkedProviders);
        Assert.Equal(1, externalLoginRepository.CreateCount);
    }

    [Fact]
    public async Task UnlinkExternalLoginAsyncRejectsRemovingFinalSignInMethod()
    {
        var user = CreateSocialUser();
        var externalLoginRepository = new CredentialFakeExternalLoginRepository(
            user,
            ExternalLoginProviders.Google,
            "google-subject");

        var service = CreateService(user, externalLoginRepository);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.UnlinkExternalLoginAsync(
                new UnlinkExternalLoginCommand(
                    ExternalLoginProviders.Google,
                    new AccountReauthenticationProof(null, ExternalLoginProviders.Google, "token")),
                CancellationToken.None));

        Assert.Equal(
            AccountAuthenticationErrorCodes.FinalSignInMethodCannotBeRemoved,
            exception.ErrorCode);
    }

    [Fact]
    public async Task CreatePasswordAsyncSetsHashForSocialUserWithVerifiedEmail()
    {
        var user = CreateSocialUser();
        user.MarkEmailVerified(DateTime.UtcNow);
        var repository = new CredentialFakeUserRepository(user);
        var externalLoginRepository = new CredentialFakeExternalLoginRepository(
            user,
            ExternalLoginProviders.Google,
            "google-subject");
        var verifier = new CredentialFakeSocialVerifier(
            ExternalLoginProviders.Google,
            new VerifiedSocialIdentity(
                ExternalLoginProviders.Google,
                "google-subject",
                user.Email,
                true,
                user.DisplayName));

        var service = CreateService(
            user,
            externalLoginRepository,
            [verifier],
            repository);

        await service.CreatePasswordAsync(
            new CreatePasswordCommand("AnotherPassword123", ExternalLoginProviders.Google, "token"));

        Assert.True(user.HasPassword);
    }

    [Fact]
    public async Task RequestEmailChangeAsyncDoesNotMutateEmailBeforeVerification()
    {
        var user = CreatePasswordUser();
        user.MarkEmailVerified(DateTime.UtcNow);
        var repository = new CredentialFakeUserRepository(user);
        var tokenRepository = new CredentialFakeEmailVerificationTokenRepository();
        var service = CreateService(
            user,
            new CredentialFakeExternalLoginRepository(),
            [],
            repository,
            tokenRepository);

        await service.RequestEmailChangeAsync(
            new RequestEmailChangeCommand(
                "new@example.com",
                new AccountReauthenticationProof("StrongPassword123", null, null),
                "en-US"));

        Assert.Equal("user@example.com", user.Email);
        Assert.Single(tokenRepository.CreatedTokens);
        Assert.Equal("new@example.com", tokenRepository.CreatedTokens[0].PendingEmail);
    }

    private static UserCredentialMethodsService CreateService(
        User user,
        CredentialFakeExternalLoginRepository externalLoginRepository,
        params CredentialFakeSocialVerifier[] verifiers) =>
        CreateService(user, externalLoginRepository, verifiers, new CredentialFakeUserRepository(user), null);

    private static UserCredentialMethodsService CreateService(
        User user,
        CredentialFakeExternalLoginRepository externalLoginRepository,
        CredentialFakeSocialVerifier[] verifiers,
        CredentialFakeUserRepository userRepository,
        CredentialFakeEmailVerificationTokenRepository? emailVerificationTokenRepository = null)
    {
        var passwordHasher = new CredentialFakePasswordHasher();
        var accountReauthentication = new AccountReauthenticationService(
            externalLoginRepository,
            passwordHasher,
            verifiers);

        emailVerificationTokenRepository ??= new CredentialFakeEmailVerificationTokenRepository();

        return new UserCredentialMethodsService(
            new CredentialFakeCurrentUser(user.Id),
            userRepository,
            externalLoginRepository,
            accountReauthentication,
            passwordHasher,
            new FakeAuthenticationSessionService(),
            emailVerificationTokenRepository,
            new CredentialFakeDeliverySecretProtector(),
            new CredentialFakeDeliveryEnqueuer(),
            Options.Create(new EmailVerificationOptions { TokenLifetimeMinutes = 60 }),
            NullLogger<UserCredentialMethodsService>.Instance);
    }

    private static User CreatePasswordUser() =>
        User.Create(Guid.NewGuid(), "user@example.com", "hash", "User", DateTime.UtcNow);

    private static User CreateSocialUser() =>
        User.CreateFromExternalIdentity(
            Guid.NewGuid(),
            "social@example.com",
            "Social",
            DateTime.UtcNow);

    private sealed class CredentialFakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
    }

    private sealed class CredentialFakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password) => $"hash:{password}";

        public bool VerifyPassword(string password, string passwordHash) =>
            password == "StrongPassword123";
    }

    private sealed class CredentialFakeUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(user.Id == id ? user : null);

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(user.Id == id ? user : null);

        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(user.SecurityStamp);

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(user.NormalizedEmail == normalizedEmail ? user : null);

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default) =>
            Task.FromResult(user);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class CredentialFakeExternalLoginRepository : IUserExternalLoginRepository
    {
        private readonly Dictionary<(string Provider, string Subject), User> _links = new();

        public CredentialFakeExternalLoginRepository(
            User? linkedUser = null,
            string? provider = null,
            string? subject = null)
        {
            if (linkedUser is not null && provider is not null && subject is not null)
            {
                _links[(provider, subject)] = linkedUser;
            }
        }

        public int CreateCount { get; private set; }

        public Task<User?> GetUserByProviderAndSubjectAsync(
            string provider,
            string providerSubject,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_links.TryGetValue((provider, providerSubject), out var user) ? user : null);

        public Task<UserExternalLogin> CreateAsync(
            UserExternalLogin externalLogin,
            CancellationToken cancellationToken = default)
        {
            CreateCount++;
            _links[(externalLogin.Provider, externalLogin.ProviderSubject)] = new User { Id = externalLogin.UserId };
            return Task.FromResult(externalLogin);
        }

        public Task<IReadOnlyList<string>> GetProvidersForUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var providers = _links
                .Where(link => link.Value.Id == userId)
                .Select(link => link.Key.Provider)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            return Task.FromResult<IReadOnlyList<string>>(providers);
        }

        public Task<bool> DeleteAsync(Guid userId, string provider, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class CredentialFakeSocialVerifier(string provider, VerifiedSocialIdentity identity)
        : ISocialIdentityTokenVerifier
    {
        public string Provider => provider;

        public Task<VerifiedSocialIdentity> VerifyIdentityTokenAsync(
            string identityToken,
            CancellationToken cancellationToken = default) => Task.FromResult(identity);
    }

    private sealed class CredentialFakeEmailVerificationTokenRepository : IEmailVerificationTokenRepository
    {
        public List<EmailVerificationToken> CreatedTokens { get; } = [];

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
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<EmailVerificationDeliveryTarget?> GetDeliveryTargetAsync(
            Guid tokenId,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EmailVerificationDeliveryTarget?>(null);

        public Task CompleteDeliveryAsync(Guid tokenId, DateTime utcNow, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class CredentialFakeDeliverySecretProtector : IEmailVerificationDeliverySecretProtector
    {
        public string Protect(string rawToken) => rawToken;
        public string Unprotect(string protectedSecret) => protectedSecret;
    }

    private sealed class CredentialFakeDeliveryEnqueuer : IEmailVerificationDeliveryEnqueuer
    {
        public Task EnqueueAsync(Guid tokenId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
