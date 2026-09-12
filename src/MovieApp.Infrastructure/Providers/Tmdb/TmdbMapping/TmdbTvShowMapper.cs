using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

internal static class TmdbTvShowMapper
{
    internal static TvShowProviderSummary ToSummary(TmdbTvSearchResultJson result)
    {
        return new TvShowProviderSummary(
            ExternalId: TmdbExternalIdFormatter.ToExternalId(result.Id),
            TmdbId: result.Id,
            TvdbId: null,
            ImdbId: null,
            Title: result.Name ?? string.Empty,
            OriginalTitle: result.OriginalName,
            Overview: result.Overview,
            FirstAirDate: TmdbMovieMapper.ParseReleaseDate(result.FirstAirDate),
            PosterPath: TmdbMovieMapper.NormalizeImagePath(result.PosterPath),
            BackdropPath: TmdbMovieMapper.NormalizeImagePath(result.BackdropPath),
            OriginalLanguage: result.OriginalLanguage,
            VoteAverage: result.VoteAverage,
            VoteCount: result.VoteCount);
    }

    internal static TvShowProviderDetails ToDetails(TmdbTvDetailsResponseJson details)
    {
        var seasons = details.Seasons
            .Where(season => season.SeasonNumber > 0)
            .OrderBy(season => season.SeasonNumber)
            .Select(ToSeasonSummary)
            .ToList();

        return new TvShowProviderDetails(
            ExternalId: TmdbExternalIdFormatter.ToExternalId(details.Id),
            TmdbId: details.Id,
            TvdbId: details.ExternalIds?.TvdbId,
            ImdbId: details.ExternalIds?.ImdbId,
            Title: details.Name ?? string.Empty,
            OriginalTitle: details.OriginalName,
            Overview: details.Overview,
            FirstAirDate: TmdbMovieMapper.ParseReleaseDate(details.FirstAirDate),
            LastAirDate: TmdbMovieMapper.ParseReleaseDate(details.LastAirDate),
            PosterPath: TmdbMovieMapper.NormalizeImagePath(details.PosterPath),
            BackdropPath: TmdbMovieMapper.NormalizeImagePath(details.BackdropPath),
            OriginalLanguage: details.OriginalLanguage,
            VoteAverage: details.VoteAverage,
            VoteCount: details.VoteCount,
            Status: details.Status ?? string.Empty,
            Genres: details.Genres
                .Where(genre => !string.IsNullOrWhiteSpace(genre.Name))
                .Select(genre => genre.Name!)
                .ToList(),
            Seasons: seasons);
    }

    internal static SeasonProviderDetails ToSeasonDetails(
        string externalTvShowId,
        TmdbTvSeasonDetailsResponseJson season)
    {
        var episodes = season.Episodes
            .OrderBy(episode => episode.EpisodeNumber)
            .Select(episode => ToEpisodeDetails(externalTvShowId, season.SeasonNumber, episode))
            .ToList();

        return new SeasonProviderDetails(
            ExternalTvShowId: externalTvShowId,
            TmdbId: season.Id,
            TvdbId: null,
            SeasonNumber: season.SeasonNumber,
            Name: season.Name,
            Overview: season.Overview,
            AirDate: TmdbMovieMapper.ParseReleaseDate(season.AirDate),
            EpisodeCount: season.EpisodeCount ?? episodes.Count,
            PosterPath: TmdbMovieMapper.NormalizeImagePath(season.PosterPath),
            Episodes: episodes);
    }

    internal static EpisodeProviderDetails ToEpisodeDetails(
        string externalTvShowId,
        int seasonNumber,
        TmdbTvEpisodeJson episode)
    {
        return new EpisodeProviderDetails(
            ExternalTvShowId: externalTvShowId,
            TmdbId: episode.Id,
            TvdbId: episode.ExternalIds?.TvdbId,
            ImdbId: episode.ExternalIds?.ImdbId,
            SeasonNumber: seasonNumber,
            EpisodeNumber: episode.EpisodeNumber,
            Name: episode.Name,
            Overview: episode.Overview,
            AirDate: TmdbMovieMapper.ParseReleaseDate(episode.AirDate),
            RuntimeMinutes: episode.Runtime,
            StillPath: TmdbMovieMapper.NormalizeImagePath(episode.StillPath),
            VoteAverage: episode.VoteAverage,
            VoteCount: episode.VoteCount);
    }

    private static SeasonProviderSummary ToSeasonSummary(TmdbTvSeasonSummaryJson season) =>
        new(
            season.SeasonNumber,
            season.Name,
            TmdbMovieMapper.ParseReleaseDate(season.AirDate),
            season.EpisodeCount,
            TmdbMovieMapper.NormalizeImagePath(season.PosterPath));
}
