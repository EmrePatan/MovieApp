namespace MovieApp.Domain.Entities;

public sealed class UserPrivacySettings
{
    public Guid UserId { get; set; }

    public bool IsPrivate { get; set; }

    public bool ShowWatched { get; set; } = true;

    public bool ShowRatings { get; set; } = true;

    public bool ShowFavorites { get; set; } = true;

    public bool ShowReviews { get; set; } = true;

    public DateTime UpdatedAt { get; set; }

    public static UserPrivacySettings CreateDefault(Guid userId, DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        return new UserPrivacySettings
        {
            UserId = userId,
            IsPrivate = false,
            ShowWatched = true,
            ShowRatings = true,
            ShowFavorites = true,
            ShowReviews = true,
            UpdatedAt = utcNow
        };
    }

    public void Update(
        bool isPrivate,
        bool showWatched,
        bool showRatings,
        bool showFavorites,
        bool showReviews,
        DateTime utcNow)
    {
        IsPrivate = isPrivate;
        ShowWatched = showWatched;
        ShowRatings = showRatings;
        ShowFavorites = showFavorites;
        ShowReviews = showReviews;
        UpdatedAt = utcNow;
    }
}
