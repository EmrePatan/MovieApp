using MovieApp.Application.Common;
using MovieApp.Application.Models.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

internal static class TmdbMovieMapper
{
    internal static MovieProviderSummary ToSummary(TmdbMovieSearchResultJson result)
    {
        return new MovieProviderSummary(
            ExternalId: TmdbExternalIdFormatter.ToExternalId(result.Id),
            TmdbId: result.Id,
            TvdbId: null,
            ImdbId: null,
            Title: result.Title ?? string.Empty,
            Overview: result.Overview,
            ReleaseDate: ParseReleaseDate(result.ReleaseDate),
            PosterPath: NormalizeImagePath(result.PosterPath),
            VoteAverage: result.VoteAverage,
            VoteCount: result.VoteCount,
            OriginalTitle: result.OriginalTitle);
    }

    internal static MovieProviderDetails ToDetails(TmdbMovieDetailsResponseJson details)
    {
        return new MovieProviderDetails(
            ExternalId: TmdbExternalIdFormatter.ToExternalId(details.Id),
            TmdbId: details.Id,
            TvdbId: details.ExternalIds?.TvdbId,
            ImdbId: ImdbIdNormalizer.Normalize(details.ExternalIds?.ImdbId ?? details.ImdbId),
            Title: details.Title ?? string.Empty,
            OriginalTitle: details.OriginalTitle,
            Overview: details.Overview,
            ReleaseDate: ParseReleaseDate(details.ReleaseDate),
            RuntimeMinutes: details.Runtime,
            PosterPath: NormalizeImagePath(details.PosterPath),
            BackdropPath: NormalizeImagePath(details.BackdropPath),
            OriginalLanguage: details.OriginalLanguage,
            VoteAverage: details.VoteAverage,
            VoteCount: details.VoteCount,
            Genres: details.Genres
                .Where(genre => !string.IsNullOrWhiteSpace(genre.Name))
                .Select(genre => genre.Name!)
                .ToList(),
            TmdbCollectionId: details.BelongsToCollection?.Id,
            CollectionName: details.BelongsToCollection?.Name,
            CollectionPosterPath: NormalizeImagePath(details.BelongsToCollection?.PosterPath),
            CollectionBackdropPath: NormalizeImagePath(details.BelongsToCollection?.BackdropPath));
    }

    internal static MovieProviderSearchResult ToSearchResult(
        TmdbMovieSearchResponseJson response,
        int requestedPage)
    {
        var results = response.Results
            .Select(ToSummary)
            .ToList();

        return new MovieProviderSearchResult(
            results,
            response.Page == 0 ? requestedPage : response.Page,
            TmdbSearchDefaults.ResultsPerPage,
            response.TotalResults,
            response.TotalPages);
    }

    internal static DateOnly? ParseReleaseDate(string? releaseDate)
    {
        if (string.IsNullOrWhiteSpace(releaseDate))
        {
            return null;
        }

        return DateOnly.TryParse(releaseDate, out var parsedDate)
            ? parsedDate
            : null;
    }

    internal static string? NormalizeImagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return path.StartsWith('/') ? path : $"/{path}";
    }
}
