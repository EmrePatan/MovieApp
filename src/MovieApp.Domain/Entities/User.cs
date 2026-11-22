using MovieApp.Domain.Users;

namespace MovieApp.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string? PendingEmail { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? PasswordHash { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? Bio { get; set; }

    public string? SocialX { get; set; }

    public string? SocialInstagram { get; set; }

    public string? SocialSnapchat { get; set; }

    public string? SocialTiktok { get; set; }

    public string? SocialYoutube { get; set; }

    public string? SocialTwitch { get; set; }

    public string? SocialThreads { get; set; }

    public string? SocialWebsite { get; set; }

    public string? CustomAvatarStorageKey { get; set; }

    public string? ProfileCoverPosterPath { get; set; }

    public string? CustomProfileCoverStorageKey { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public DateTime? EmailVerifiedAtUtc { get; set; }

    public Guid SecurityStamp { get; set; }

    public ICollection<Favorite> Favorites { get; set; } = [];

    public ICollection<Watchlist> Watchlists { get; set; } = [];

    public ICollection<Rating> Ratings { get; set; } = [];

    public ICollection<Review> Reviews { get; set; } = [];

    public ICollection<WatchedMovie> WatchedMovies { get; set; } = [];

    public ICollection<WatchedEpisode> WatchedEpisodes { get; set; } = [];

    public ICollection<SearchHistory> SearchHistories { get; set; } = [];

    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = [];

    public ICollection<EmailVerificationToken> EmailVerificationTokens { get; set; } = [];

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    public ICollection<CatalogFollow> CatalogFollows { get; set; } = [];

    public ICollection<UserReleaseNotification> ReleaseNotifications { get; set; } = [];

    public ICollection<PushDevice> PushDevices { get; set; } = [];

    public ICollection<UserExternalLogin> ExternalLogins { get; set; } = [];

    public bool HasPassword => !string.IsNullOrEmpty(PasswordHash);

    public bool IsEmailVerified => EmailVerifiedAtUtc.HasValue;

    public static User Create(
        Guid id,
        string email,
        string passwordHash,
        string displayName,
        DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var normalizedEmail = UserEmailNormalizer.Normalize(email);
        var userName = CreateUserNameFromEmail(normalizedEmail);

        return new User
        {
            Id = id,
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            UserName = userName,
            PasswordHash = passwordHash,
            DisplayName = displayName.Trim(),
            IsActive = true,
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public static User CreateFromExternalIdentity(
        Guid id,
        string email,
        string displayName,
        DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var normalizedEmail = UserEmailNormalizer.Normalize(email);
        var userName = CreateUserNameFromEmail(normalizedEmail);

        return new User
        {
            Id = id,
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            UserName = userName,
            PasswordHash = null,
            DisplayName = displayName.Trim(),
            IsActive = true,
            SecurityStamp = Guid.NewGuid(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
    }

    public void SetCustomAvatarStorageKey(string storageKey, DateTime utcNow)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Inactive users cannot update their avatar.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        CustomAvatarStorageKey = storageKey.Trim();
        UpdatedAt = utcNow;
    }

    public void ClearCustomAvatarStorageKey(DateTime utcNow)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Inactive users cannot update their avatar.");
        }

        CustomAvatarStorageKey = null;
        UpdatedAt = utcNow;
    }

    public void SetProfileCoverPosterPath(string? posterPath, DateTime utcNow)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Inactive users cannot update their profile cover.");
        }

        ProfileCoverPosterPath = string.IsNullOrWhiteSpace(posterPath) ? null : posterPath.Trim();
        UpdatedAt = utcNow;
    }

    public void SetCustomProfileCoverStorageKey(string storageKey, DateTime utcNow)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Inactive users cannot update their profile cover.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        CustomProfileCoverStorageKey = storageKey.Trim();
        ProfileCoverPosterPath = null;
        UpdatedAt = utcNow;
    }

    public void ClearCustomProfileCoverStorageKey(DateTime utcNow)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Inactive users cannot update their profile cover.");
        }

        CustomProfileCoverStorageKey = null;
        UpdatedAt = utcNow;
    }

    public void UpdateDisplayName(string displayName, DateTime utcNow)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Inactive users cannot update their profile.");
        }

        var trimmedDisplayName = displayName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedDisplayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        if (trimmedDisplayName.Length > 100)
        {
            throw new ArgumentException("Display name must not exceed 100 characters.", nameof(displayName));
        }

        DisplayName = trimmedDisplayName;
        UpdatedAt = utcNow;
    }

    public void UpdateProfile(
        string displayName,
        string? bio,
        string? socialX,
        string? socialInstagram,
        string? socialSnapchat,
        string? socialTiktok,
        string? socialYoutube,
        string? socialTwitch,
        string? socialThreads,
        string? socialWebsite,
        DateTime utcNow)
    {
        UpdateDisplayName(displayName, utcNow);
        Bio = NormalizeOptionalText(bio, 300);
        SocialX = NormalizeOptionalText(socialX, 64);
        SocialInstagram = NormalizeOptionalText(socialInstagram, 64);
        SocialSnapchat = NormalizeOptionalText(socialSnapchat, 64);
        SocialTiktok = NormalizeOptionalText(socialTiktok, 64);
        SocialYoutube = NormalizeOptionalText(socialYoutube, 64);
        SocialTwitch = NormalizeOptionalText(socialTwitch, 64);
        SocialThreads = NormalizeOptionalText(socialThreads, 64);
        SocialWebsite = NormalizeOptionalText(socialWebsite, 200);
        UpdatedAt = utcNow;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value must not exceed {maxLength} characters.");
        }

        return trimmed;
    }

    public void ChangeEmail(string email, string normalizedEmail, DateTime utcNow)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Inactive users cannot change their email.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);

        Email = email.Trim();
        NormalizedEmail = normalizedEmail;
        EmailVerifiedAtUtc = null;
        RotateSecurityStamp(utcNow);
    }

    public void SetPendingEmailChange(string pendingEmail, DateTime utcNow)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Inactive users cannot request an email change.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(pendingEmail);

        PendingEmail = pendingEmail.Trim();
        UpdatedAt = utcNow;
    }

    public void ClearPendingEmailChange(DateTime utcNow)
    {
        PendingEmail = null;
        UpdatedAt = utcNow;
    }

    public void ChangePassword(string passwordHash, DateTime utcNow)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Inactive users cannot change their password.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        PasswordHash = passwordHash;
        RotateSecurityStamp(utcNow);
    }

    public void SetInitialDisplayNameIfEmpty(string? displayName, DateTime utcNow)
    {
        if (!IsActive || string.IsNullOrWhiteSpace(displayName) || !string.IsNullOrWhiteSpace(DisplayName))
        {
            return;
        }

        var trimmedDisplayName = displayName.Trim();
        if (trimmedDisplayName.Length > 100)
        {
            trimmedDisplayName = trimmedDisplayName[..100];
        }

        DisplayName = trimmedDisplayName;
        UpdatedAt = utcNow;
    }

    public void RotateSecurityStamp(DateTime utcNow)
    {
        SecurityStamp = Guid.NewGuid();
        UpdatedAt = utcNow;
    }

    public void MarkEmailVerified(DateTime utcNow)
    {
        EmailVerifiedAtUtc = utcNow;
        UpdatedAt = utcNow;
    }

    public void RecordSuccessfulLogin(DateTime utcNow)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Inactive users cannot record a successful login.");
        }

        LastLoginAt = utcNow;
        UpdatedAt = utcNow;
    }

    private static string CreateUserNameFromEmail(string normalizedEmail)
    {
        var atIndex = normalizedEmail.IndexOf('@');
        return atIndex > 0 ? normalizedEmail[..atIndex] : normalizedEmail;
    }
}
