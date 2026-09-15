using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Abstractions.Providers;

public interface ITvShowDataProvider
{
    Task<TvShowProviderSearchResult> SearchTvShowsAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
        DiscoverProviderCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<TvShowProviderSearchResult> AdvancedDiscoverTvShowsAsync(
        AdvancedDiscoverProviderCriteria criteria,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    Task<TvShowProviderDetails?> GetTvShowAsync(
        string externalId,
        CancellationToken cancellationToken = default);

    Task<SeasonProviderDetails?> GetSeasonAsync(
        string externalTvShowId,
        int seasonNumber,
        CancellationToken cancellationToken = default);

    Task<EpisodeProviderDetails?> GetEpisodeAsync(
        string externalTvShowId,
        int seasonNumber,
        int episodeNumber,
        CancellationToken cancellationToken = default);
}
