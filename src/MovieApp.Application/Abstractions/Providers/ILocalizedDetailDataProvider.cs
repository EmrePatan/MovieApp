using MovieApp.Application.Models.Localization;

namespace MovieApp.Application.Abstractions.Providers;

public interface ILocalizedDetailDataProvider
{
    Task<MovieDetailLocalizationData?> GetMovieLocalizationAsync(
        int tmdbId,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<TvShowDetailLocalizationData?> GetTvShowLocalizationAsync(
        int tmdbId,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<PersonDetailLocalizationData?> GetPersonLocalizationAsync(
        int tmdbPersonId,
        string contentLocale,
        CancellationToken cancellationToken = default);

    Task<CollectionDetailLocalizationData?> GetCollectionLocalizationAsync(
        int tmdbCollectionId,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
