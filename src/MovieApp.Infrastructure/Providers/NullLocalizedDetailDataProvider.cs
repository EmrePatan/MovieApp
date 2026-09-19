using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Localization;

namespace MovieApp.Infrastructure.Providers;

internal sealed class NullLocalizedDetailDataProvider : ILocalizedDetailDataProvider
{
    public Task<MovieDetailLocalizationData?> GetMovieLocalizationAsync(
        int tmdbId,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<MovieDetailLocalizationData?>(null);

    public Task<TvShowDetailLocalizationData?> GetTvShowLocalizationAsync(
        int tmdbId,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<TvShowDetailLocalizationData?>(null);

    public Task<TvSeasonDetailLocalizationData?> GetTvSeasonLocalizationAsync(
        int tmdbTvId,
        int seasonNumber,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<TvSeasonDetailLocalizationData?>(null);

    public Task<PersonDetailLocalizationData?> GetPersonLocalizationAsync(
        int tmdbPersonId,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<PersonDetailLocalizationData?>(null);

    public Task<CollectionDetailLocalizationData?> GetCollectionLocalizationAsync(
        int tmdbCollectionId,
        string contentLocale,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<CollectionDetailLocalizationData?>(null);
}
