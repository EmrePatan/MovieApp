namespace MovieApp.Application.Abstractions.Social;

public sealed record SocialPrivacyResult(bool IsPrivate, bool ShowWatched, bool ShowRatings, bool ShowFavorites, bool ShowReviews);

public interface ISocialPrivacyService
{
    Task<SocialPrivacyResult> GetAsync(CancellationToken cancellationToken = default);
    Task<SocialPrivacyResult> UpdateAsync(SocialPrivacyResult settings, CancellationToken cancellationToken = default);
}
