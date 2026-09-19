using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Services.Identity;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Users;

namespace MovieApp.UnitTests.Identity;

public sealed class SocialAuthServiceTests
{
    [Fact]
    public async Task AuthenticateAsyncCreatesNewGoogleUserAndIssuesJwt()
    {
        var verifier = new FakeSocialIdentityTokenVerifier(
            ExternalLoginProviders.Google,
            new VerifiedSocialIdentity(
                ExternalLoginProviders.Google,
                "google-subject-1",
                "google.user@example.com",
                true,
                "Google User"));

        var userRepository = new FakeUserRepository();
        var externalLoginRepository = new FakeExternalLoginRepository();
        var service = CreateService(userRepository, externalLoginRepository, verifier);

        var result = await service.AuthenticateAsync(
            new SocialAuthRequest(ExternalLoginProviders.Google, "token"));

        Assert.Equal("token-value", result.AccessToken);
        Assert.Equal("google.user@example.com", result.User.Email);
        Assert.Equal(1, userRepository.CreateCount);
        Assert.Equal(1, externalLoginRepository.CreateCount);
    }

    [Fact]
    public async Task AuthenticateAsyncMarksProviderVerifiedEmailAsVerified()
    {
        var verifier = new FakeSocialIdentityTokenVerifier(
            ExternalLoginProviders.Google,
            new VerifiedSocialIdentity(
                ExternalLoginProviders.Google,
                "google-subject-verified",
                "verified.user@example.com",
                true,
                "Verified User"));

        var userRepository = new FakeUserRepository();
        var externalLoginRepository = new FakeExternalLoginRepository();
        var service = CreateService(userRepository, externalLoginRepository, verifier);

        await service.AuthenticateAsync(
            new SocialAuthRequest(ExternalLoginProviders.Google, "token"));

        var createdUser = userRepository.GetLastCreatedUser();
        Assert.NotNull(createdUser);
        Assert.True(createdUser.IsEmailVerified);
    }

    [Fact]
    public async Task AuthenticateAsyncLogsInExistingGoogleExternalIdentity()
    {
        var existingUser = User.CreateFromExternalIdentity(
            Guid.NewGuid(),
            "linked@example.com",
            "Linked User",
            DateTime.UtcNow);

        var verifier = new FakeSocialIdentityTokenVerifier(
            ExternalLoginProviders.Google,
            new VerifiedSocialIdentity(
                ExternalLoginProviders.Google,
                "google-subject-existing",
                "linked@example.com",
                true,
                "Linked User"));

        var userRepository = new FakeUserRepository(existingUser);
        var externalLoginRepository = new FakeExternalLoginRepository(existingUser, ExternalLoginProviders.Google, "google-subject-existing");
        var service = CreateService(userRepository, externalLoginRepository, verifier);

        var result = await service.AuthenticateAsync(
            new SocialAuthRequest(ExternalLoginProviders.Google, "token"));

        Assert.Equal(existingUser.Id, result.User.Id);
        Assert.Equal(0, userRepository.CreateCount);
        Assert.Equal(1, userRepository.UpdateCount);
    }

    [Fact]
    public async Task AuthenticateAsyncCreatesNewAppleUserAndIssuesJwt()
    {
        var verifier = new FakeSocialIdentityTokenVerifier(
            ExternalLoginProviders.Apple,
            new VerifiedSocialIdentity(
                ExternalLoginProviders.Apple,
                "apple-subject-1",
                "apple.user@example.com",
                true,
                null));

        var userRepository = new FakeUserRepository();
        var externalLoginRepository = new FakeExternalLoginRepository();
        var service = CreateService(userRepository, externalLoginRepository, verifier);

        var result = await service.AuthenticateAsync(
            new SocialAuthRequest(ExternalLoginProviders.Apple, "token"));

        Assert.Equal("apple.user@example.com", result.User.Email);
        Assert.Equal(1, userRepository.CreateCount);
    }

    [Fact]
    public async Task AuthenticateAsyncRejectsInvalidProvider()
    {
        var service = CreateService(new FakeUserRepository(), new FakeExternalLoginRepository());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.AuthenticateAsync(new SocialAuthRequest("facebook", "token")));
    }

    [Fact]
    public async Task AuthenticateAsyncRejectsInvalidToken()
    {
        var verifier = new FakeSocialIdentityTokenVerifier(
            ExternalLoginProviders.Google,
            shouldThrowAuthentication: true);

        var service = CreateService(new FakeUserRepository(), new FakeExternalLoginRepository(), verifier);

        await Assert.ThrowsAsync<AuthenticationException>(() =>
            service.AuthenticateAsync(new SocialAuthRequest(ExternalLoginProviders.Google, "bad-token")));
    }

    [Fact]
    public async Task AuthenticateAsyncDoesNotAutoLinkUnverifiedEmail()
    {
        var passwordUser = User.Create(
            Guid.NewGuid(),
            "shared@example.com",
            "hashed-password",
            "Password User",
            DateTime.UtcNow);

        var verifier = new FakeSocialIdentityTokenVerifier(
            ExternalLoginProviders.Google,
            new VerifiedSocialIdentity(
                ExternalLoginProviders.Google,
                "google-subject-new",
                "shared@example.com",
                false,
                "Google User"));

        var userRepository = new FakeUserRepository(passwordUser);
        var externalLoginRepository = new FakeExternalLoginRepository();
        var service = CreateService(userRepository, externalLoginRepository, verifier);

        var result = await service.AuthenticateAsync(
            new SocialAuthRequest(ExternalLoginProviders.Google, "token"));

        Assert.Equal(1, userRepository.CreateCount);
        Assert.NotEqual(passwordUser.Id, result.User.Id);
    }

    [Fact]
    public async Task AuthenticateAsyncRejectsVerifiedEmailPasswordAccountConflict()
    {
        var passwordUser = User.Create(
            Guid.NewGuid(),
            "shared@example.com",
            "hashed-password",
            "Password User",
            DateTime.UtcNow);

        var verifier = new FakeSocialIdentityTokenVerifier(
            ExternalLoginProviders.Google,
            new VerifiedSocialIdentity(
                ExternalLoginProviders.Google,
                "google-subject-new",
                "shared@example.com",
                true,
                "Google User"));

        var userRepository = new FakeUserRepository(passwordUser);
        var externalLoginRepository = new FakeExternalLoginRepository();
        var service = CreateService(userRepository, externalLoginRepository, verifier);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.AuthenticateAsync(new SocialAuthRequest(ExternalLoginProviders.Google, "token")));
    }

    [Fact]
    public async Task AuthenticateAsyncLinksVerifiedEmailToExistingSocialOnlyAccount()
    {
        var socialUser = User.CreateFromExternalIdentity(
            Guid.NewGuid(),
            "social@example.com",
            "Social User",
            DateTime.UtcNow);

        var verifier = new FakeSocialIdentityTokenVerifier(
            ExternalLoginProviders.Apple,
            new VerifiedSocialIdentity(
                ExternalLoginProviders.Apple,
                "apple-subject-link",
                "social@example.com",
                true,
                "Apple User"));

        var userRepository = new FakeUserRepository(socialUser);
        var externalLoginRepository = new FakeExternalLoginRepository();
        var service = CreateService(userRepository, externalLoginRepository, verifier);

        var result = await service.AuthenticateAsync(
            new SocialAuthRequest(ExternalLoginProviders.Apple, "token"));

        Assert.Equal(socialUser.Id, result.User.Id);
        Assert.Equal(1, externalLoginRepository.CreateCount);
        Assert.Equal(0, userRepository.CreateCount);
    }

    [Fact]
    public async Task AuthenticateAsyncPreventsDuplicateExternalIdentityLink()
    {
        var linkedUser = User.CreateFromExternalIdentity(
            Guid.NewGuid(),
            "linked@example.com",
            "Linked User",
            DateTime.UtcNow);

        var verifier = new FakeSocialIdentityTokenVerifier(
            ExternalLoginProviders.Google,
            new VerifiedSocialIdentity(
                ExternalLoginProviders.Google,
                "google-subject-dup",
                "other@example.com",
                true,
                "Other User"));

        var userRepository = new FakeUserRepository(linkedUser);
        var externalLoginRepository = new FakeExternalLoginRepository(
            linkedUser,
            ExternalLoginProviders.Google,
            "google-subject-dup",
            throwConflictOnCreate: true);

        var service = CreateService(userRepository, externalLoginRepository, verifier);

        var result = await service.AuthenticateAsync(
            new SocialAuthRequest(ExternalLoginProviders.Google, "token"));

        Assert.Equal(linkedUser.Id, result.User.Id);
    }

    private static SocialAuthService CreateService(
        FakeUserRepository userRepository,
        FakeExternalLoginRepository externalLoginRepository,
        params FakeSocialIdentityTokenVerifier[] verifiers) =>
        new(
            externalLoginRepository,
            userRepository,
            verifiers,
            new FakeTokenService(),
            NullLogger<SocialAuthService>.Instance);

    private sealed class FakeSocialIdentityTokenVerifier : ISocialIdentityTokenVerifier
    {
        private readonly VerifiedSocialIdentity? _identity;
        private readonly bool _shouldThrowAuthentication;

        public FakeSocialIdentityTokenVerifier(string provider, VerifiedSocialIdentity identity)
        {
            Provider = provider;
            _identity = identity;
        }

        public FakeSocialIdentityTokenVerifier(string provider, bool shouldThrowAuthentication)
        {
            Provider = provider;
            _shouldThrowAuthentication = shouldThrowAuthentication;
        }

        public string Provider { get; }

        public Task<VerifiedSocialIdentity> VerifyIdentityTokenAsync(
            string identityToken,
            CancellationToken cancellationToken = default)
        {
            if (_shouldThrowAuthentication)
            {
                throw new AuthenticationException("Invalid token.");
            }

            return Task.FromResult(_identity!);
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<string, User> _usersByEmail = new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, User> _usersById = new();

        public FakeUserRepository(User? seedUser = null)
        {
            if (seedUser is not null)
            {
                _usersByEmail[seedUser.NormalizedEmail] = seedUser;
                _usersById[seedUser.Id] = seedUser;
            }
        }

        public int CreateCount { get; private set; }

        public int UpdateCount { get; private set; }

        private User? _lastCreatedUser;

        public User? GetLastCreatedUser() => _lastCreatedUser;

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_usersById.TryGetValue(id, out var user) ? user : null);

        public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
            GetByIdAsync(id, cancellationToken);

        public Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(_usersById.TryGetValue(id, out var user) ? user.SecurityStamp : null);

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(_usersByEmail.TryGetValue(normalizedEmail, out var user) ? user : null);

        public Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(_usersByEmail.ContainsKey(normalizedEmail));

        public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
        {
            CreateCount++;
            _lastCreatedUser = user;
            _usersByEmail[user.NormalizedEmail] = user;
            _usersById[user.Id] = user;
            return Task.FromResult(user);
        }

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            UpdateCount++;
            _usersByEmail[user.NormalizedEmail] = user;
            _usersById[user.Id] = user;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeExternalLoginRepository : IUserExternalLoginRepository
    {
        private readonly Dictionary<(string Provider, string Subject), User> _links = new();
        private readonly bool _throwConflictOnCreate;

        public FakeExternalLoginRepository(
            User? linkedUser = null,
            string? provider = null,
            string? subject = null,
            bool throwConflictOnCreate = false)
        {
            _throwConflictOnCreate = throwConflictOnCreate;

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

            if (_throwConflictOnCreate && _links.ContainsKey((externalLogin.Provider, externalLogin.ProviderSubject)))
            {
                throw new ConflictException("Duplicate external login.");
            }

            if (_links.ContainsKey((externalLogin.Provider, externalLogin.ProviderSubject)))
            {
                throw new ConflictException("Duplicate external login.");
            }

            var user = new User { Id = externalLogin.UserId };
            _links[(externalLogin.Provider, externalLogin.ProviderSubject)] = user;
            return Task.FromResult(externalLogin);
        }
    }

    private sealed class FakeTokenService : ITokenService
    {
        public AccessTokenResult CreateAccessToken(TokenUserContext user) =>
            new("token-value", DateTime.UtcNow.AddHours(1));
    }
}
