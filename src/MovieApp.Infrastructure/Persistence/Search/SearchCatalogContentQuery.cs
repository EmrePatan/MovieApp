using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Common;
using MovieApp.Application.Services.Library;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Search;

internal static class SearchCatalogContentQuery
{
    public static IQueryable<Movie> WhereMovieMatchesSearch(
        IQueryable<Movie> query,
        ApplicationDbContext dbContext,
        SearchQueryMatch match,
        LibrarySearchTitleLanguageScope? libraryTitleScope = null)
    {
        if (match.IsEmpty)
        {
            return query;
        }

        var primary = match.Primary;
        var turkish = match.TurkishAlternate;
        var folded = match.Folded;
        var indexedTitles = IndexedTitlesInLibraryLanguageScope(dbContext, libraryTitleScope);

        return query.Where(movie =>
            (EF.Functions.ILike(movie.Title, "%" + primary + "%")
                || (turkish != null && EF.Functions.ILike(movie.Title, "%" + turkish + "%"))
                || (movie.OriginalTitle != null
                    && (EF.Functions.ILike(movie.OriginalTitle, "%" + primary + "%")
                        || (turkish != null && EF.Functions.ILike(movie.OriginalTitle, "%" + turkish + "%")))))
            || indexedTitles.Any(row =>
                row.ContentType == CatalogContentType.Movie
                && row.ContentId == movie.Id
                && (EF.Functions.ILike(row.Title, "%" + primary + "%")
                    || (turkish != null && EF.Functions.ILike(row.Title, "%" + turkish + "%"))
                    || EF.Functions.ILike(row.NormalizedTitle, "%" + folded + "%"))));
    }

    public static IQueryable<TvShow> WhereTvShowMatchesSearch(
        IQueryable<TvShow> query,
        ApplicationDbContext dbContext,
        SearchQueryMatch match,
        LibrarySearchTitleLanguageScope? libraryTitleScope = null)
    {
        if (match.IsEmpty)
        {
            return query;
        }

        var primary = match.Primary;
        var turkish = match.TurkishAlternate;
        var folded = match.Folded;
        var indexedTitles = IndexedTitlesInLibraryLanguageScope(dbContext, libraryTitleScope);

        return query.Where(tvShow =>
            (EF.Functions.ILike(tvShow.Title, "%" + primary + "%")
                || (turkish != null && EF.Functions.ILike(tvShow.Title, "%" + turkish + "%"))
                || (tvShow.OriginalTitle != null
                    && (EF.Functions.ILike(tvShow.OriginalTitle, "%" + primary + "%")
                        || (turkish != null && EF.Functions.ILike(tvShow.OriginalTitle, "%" + turkish + "%")))))
            || indexedTitles.Any(row =>
                row.ContentType == CatalogContentType.Tv
                && row.ContentId == tvShow.Id
                && (EF.Functions.ILike(row.Title, "%" + primary + "%")
                    || (turkish != null && EF.Functions.ILike(row.Title, "%" + turkish + "%"))
                    || EF.Functions.ILike(row.NormalizedTitle, "%" + folded + "%"))));
    }

    private static IQueryable<ContentSearchTitle> IndexedTitlesInLibraryLanguageScope(
        ApplicationDbContext dbContext,
        LibrarySearchTitleLanguageScope? libraryTitleScope)
    {
        if (libraryTitleScope is null)
        {
            return dbContext.ContentSearchTitles;
        }

        var primaryLanguage = libraryTitleScope.PrimaryLanguageCode;
        var includeEnglish = libraryTitleScope.IncludeEnglishLanguage;
        var primaryRegion = libraryTitleScope.PrimaryRegionCode ?? string.Empty;
        var matchCountry = primaryRegion.Length > 0;
        var englishLanguage = LibrarySearchTitleLanguageScope.EnglishLanguageCode;

        return dbContext.ContentSearchTitles.Where(row =>
            row.TitleKind == ContentSearchTitleKind.Canonical
            || row.TitleKind == ContentSearchTitleKind.Original
            || ((row.LanguageCode != null && row.LanguageCode != "")
                && (EF.Functions.ILike(row.LanguageCode, primaryLanguage)
                    || (includeEnglish && EF.Functions.ILike(row.LanguageCode, englishLanguage))))
            || (matchCountry
                && (row.LanguageCode == null || row.LanguageCode == "")
                && row.CountryCode != null
                && row.CountryCode != ""
                && EF.Functions.ILike(row.CountryCode, primaryRegion)));
    }

    public static IQueryable<SearchItemProjection> ProjectMoviesWithRelevance(
        IQueryable<Movie> query,
        ApplicationDbContext dbContext,
        string primary,
        string? turkish,
        string folded)
    {
        return query.Select(movie => new SearchItemProjection
        {
            Id = movie.Id,
            Type = "movie",
            Title = movie.Title,
            OriginalTitle = movie.OriginalTitle,
            Overview = movie.Overview,
            PosterUrl = movie.PosterPath,
            BackdropUrl = movie.BackdropPath,
            ReleaseDate = movie.ReleaseDate,
            VoteAverage = movie.VoteAverage,
            VoteCount = movie.VoteCount,
            Year = movie.ReleaseDate.HasValue ? movie.ReleaseDate.Value.Year : null,
            TmdbId = movie.TmdbId,
            KnownForDepartment = null,
            RelevanceTier = SearchPostgresFunctions.Least(
                SearchPostgresFunctions.Least(
                    (EF.Functions.ILike(movie.Title, primary) && movie.Title.Length == primary.Length)
                    || (turkish != null
                        && EF.Functions.ILike(movie.Title, turkish)
                        && movie.Title.Length == turkish.Length)
                        ? SearchBestMatchTier.DirectExactCanonical
                        : (EF.Functions.ILike(movie.Title, primary + "%")
                            || (turkish != null && EF.Functions.ILike(movie.Title, turkish + "%")))
                            ? SearchBestMatchTier.DirectPrefixCanonical
                            : (EF.Functions.ILike(movie.Title, "%" + primary + "%")
                                || (turkish != null && EF.Functions.ILike(movie.Title, "%" + turkish + "%")))
                                ? SearchBestMatchTier.DirectSubstringCanonical
                                : SearchBestMatchTier.NoMatch,
                    movie.OriginalTitle == null
                        ? SearchBestMatchTier.NoMatch
                        : (EF.Functions.ILike(movie.OriginalTitle, primary)
                           && movie.OriginalTitle.Length == primary.Length)
                          || (turkish != null
                              && EF.Functions.ILike(movie.OriginalTitle, turkish)
                              && movie.OriginalTitle.Length == turkish.Length)
                            ? SearchBestMatchTier.DirectExactOriginal
                            : (EF.Functions.ILike(movie.OriginalTitle, primary + "%")
                                || (turkish != null
                                    && EF.Functions.ILike(movie.OriginalTitle, turkish + "%")))
                                ? SearchBestMatchTier.DirectPrefixOriginal
                                : (EF.Functions.ILike(movie.OriginalTitle, "%" + primary + "%")
                                    || (turkish != null
                                        && EF.Functions.ILike(movie.OriginalTitle, "%" + turkish + "%")))
                                    ? SearchBestMatchTier.DirectSubstringOriginal
                                    : SearchBestMatchTier.NoMatch),
                dbContext.ContentSearchTitles
                    .Where(row =>
                        row.ContentType == CatalogContentType.Movie
                        && row.ContentId == movie.Id
                        && (EF.Functions.ILike(row.Title, "%" + primary + "%")
                            || (turkish != null && EF.Functions.ILike(row.Title, "%" + turkish + "%"))
                            || EF.Functions.ILike(row.NormalizedTitle, "%" + folded + "%")))
                    .Select(row => (int?)
                        (row.TitleKind == ContentSearchTitleKind.Canonical
                            ? (EF.Functions.ILike(row.Title, primary) && row.Title.Length == primary.Length)
                              || (turkish != null
                                  && EF.Functions.ILike(row.Title, turkish)
                                  && row.Title.Length == turkish.Length)
                                ? SearchBestMatchTier.DirectExactCanonical
                                : (EF.Functions.ILike(row.Title, primary + "%")
                                    || (turkish != null && EF.Functions.ILike(row.Title, turkish + "%")))
                                    ? SearchBestMatchTier.DirectPrefixCanonical
                                    : (EF.Functions.ILike(row.Title, "%" + primary + "%")
                                        || (turkish != null && EF.Functions.ILike(row.Title, "%" + turkish + "%")))
                                        ? SearchBestMatchTier.DirectSubstringCanonical
                                        : row.NormalizedTitle.Length == folded.Length
                                          && EF.Functions.ILike(row.NormalizedTitle, folded)
                                            ? SearchBestMatchTier.FoldedExactCanonical
                                            : EF.Functions.ILike(row.NormalizedTitle, folded + "%")
                                                ? SearchBestMatchTier.FoldedPrefixCanonical
                                                : EF.Functions.ILike(row.NormalizedTitle, "%" + folded + "%")
                                                    ? SearchBestMatchTier.FoldedSubstringCanonical
                                                    : SearchBestMatchTier.NoMatch
                            : row.TitleKind == ContentSearchTitleKind.Original
                                ? (EF.Functions.ILike(row.Title, primary) && row.Title.Length == primary.Length)
                                  || (turkish != null
                                      && EF.Functions.ILike(row.Title, turkish)
                                      && row.Title.Length == turkish.Length)
                                    ? SearchBestMatchTier.DirectExactOriginal
                                    : (EF.Functions.ILike(row.Title, primary + "%")
                                        || (turkish != null && EF.Functions.ILike(row.Title, turkish + "%")))
                                        ? SearchBestMatchTier.DirectPrefixOriginal
                                        : (EF.Functions.ILike(row.Title, "%" + primary + "%")
                                            || (turkish != null && EF.Functions.ILike(row.Title, "%" + turkish + "%")))
                                            ? SearchBestMatchTier.DirectSubstringOriginal
                                            : row.NormalizedTitle.Length == folded.Length
                                              && EF.Functions.ILike(row.NormalizedTitle, folded)
                                                ? SearchBestMatchTier.FoldedExactOriginal
                                                : EF.Functions.ILike(row.NormalizedTitle, folded + "%")
                                                    ? SearchBestMatchTier.FoldedPrefixOriginal
                                                    : EF.Functions.ILike(row.NormalizedTitle, "%" + folded + "%")
                                                        ? SearchBestMatchTier.FoldedSubstringOriginal
                                                        : SearchBestMatchTier.NoMatch
                                : (EF.Functions.ILike(row.Title, primary) && row.Title.Length == primary.Length)
                                  || (turkish != null
                                      && EF.Functions.ILike(row.Title, turkish)
                                      && row.Title.Length == turkish.Length)
                                    ? SearchBestMatchTier.DirectExactAlias
                                    : (EF.Functions.ILike(row.Title, primary + "%")
                                        || (turkish != null && EF.Functions.ILike(row.Title, turkish + "%")))
                                        ? SearchBestMatchTier.DirectPrefixAlias
                                        : (EF.Functions.ILike(row.Title, "%" + primary + "%")
                                            || (turkish != null && EF.Functions.ILike(row.Title, "%" + turkish + "%")))
                                            ? SearchBestMatchTier.DirectSubstringAlias
                                            : row.NormalizedTitle.Length == folded.Length
                                              && EF.Functions.ILike(row.NormalizedTitle, folded)
                                                ? SearchBestMatchTier.FoldedExactAlias
                                                : EF.Functions.ILike(row.NormalizedTitle, folded + "%")
                                                    ? SearchBestMatchTier.FoldedPrefixAlias
                                                    : EF.Functions.ILike(row.NormalizedTitle, "%" + folded + "%")
                                                        ? SearchBestMatchTier.FoldedSubstringAlias
                                                        : SearchBestMatchTier.NoMatch))
                    .Min() ?? SearchBestMatchTier.NoMatch)
        });
    }

    public static IQueryable<SearchItemProjection> ProjectTvShowsWithRelevance(
        IQueryable<TvShow> query,
        ApplicationDbContext dbContext,
        string primary,
        string? turkish,
        string folded)
    {
        return query.Select(tvShow => new SearchItemProjection
        {
            Id = tvShow.Id,
            Type = "tv",
            Title = tvShow.Title,
            OriginalTitle = tvShow.OriginalTitle,
            Overview = tvShow.Overview,
            PosterUrl = tvShow.PosterPath,
            BackdropUrl = tvShow.BackdropPath,
            ReleaseDate = tvShow.FirstAirDate,
            VoteAverage = tvShow.VoteAverage,
            VoteCount = tvShow.VoteCount,
            Year = tvShow.FirstAirDate.HasValue ? tvShow.FirstAirDate.Value.Year : null,
            TmdbId = tvShow.TmdbId,
            KnownForDepartment = null,
            RelevanceTier = SearchPostgresFunctions.Least(
                SearchPostgresFunctions.Least(
                    (EF.Functions.ILike(tvShow.Title, primary) && tvShow.Title.Length == primary.Length)
                    || (turkish != null
                        && EF.Functions.ILike(tvShow.Title, turkish)
                        && tvShow.Title.Length == turkish.Length)
                        ? SearchBestMatchTier.DirectExactCanonical
                        : (EF.Functions.ILike(tvShow.Title, primary + "%")
                            || (turkish != null && EF.Functions.ILike(tvShow.Title, turkish + "%")))
                            ? SearchBestMatchTier.DirectPrefixCanonical
                            : (EF.Functions.ILike(tvShow.Title, "%" + primary + "%")
                                || (turkish != null && EF.Functions.ILike(tvShow.Title, "%" + turkish + "%")))
                                ? SearchBestMatchTier.DirectSubstringCanonical
                                : SearchBestMatchTier.NoMatch,
                    tvShow.OriginalTitle == null
                        ? SearchBestMatchTier.NoMatch
                        : (EF.Functions.ILike(tvShow.OriginalTitle, primary)
                           && tvShow.OriginalTitle.Length == primary.Length)
                          || (turkish != null
                              && EF.Functions.ILike(tvShow.OriginalTitle, turkish)
                              && tvShow.OriginalTitle.Length == turkish.Length)
                            ? SearchBestMatchTier.DirectExactOriginal
                            : (EF.Functions.ILike(tvShow.OriginalTitle, primary + "%")
                                || (turkish != null
                                    && EF.Functions.ILike(tvShow.OriginalTitle, turkish + "%")))
                                ? SearchBestMatchTier.DirectPrefixOriginal
                                : (EF.Functions.ILike(tvShow.OriginalTitle, "%" + primary + "%")
                                    || (turkish != null
                                        && EF.Functions.ILike(tvShow.OriginalTitle, "%" + turkish + "%")))
                                    ? SearchBestMatchTier.DirectSubstringOriginal
                                    : SearchBestMatchTier.NoMatch),
                dbContext.ContentSearchTitles
                    .Where(row =>
                        row.ContentType == CatalogContentType.Tv
                        && row.ContentId == tvShow.Id
                        && (EF.Functions.ILike(row.Title, "%" + primary + "%")
                            || (turkish != null && EF.Functions.ILike(row.Title, "%" + turkish + "%"))
                            || EF.Functions.ILike(row.NormalizedTitle, "%" + folded + "%")))
                    .Select(row => (int?)
                        (row.TitleKind == ContentSearchTitleKind.Canonical
                            ? (EF.Functions.ILike(row.Title, primary) && row.Title.Length == primary.Length)
                              || (turkish != null
                                  && EF.Functions.ILike(row.Title, turkish)
                                  && row.Title.Length == turkish.Length)
                                ? SearchBestMatchTier.DirectExactCanonical
                                : (EF.Functions.ILike(row.Title, primary + "%")
                                    || (turkish != null && EF.Functions.ILike(row.Title, turkish + "%")))
                                    ? SearchBestMatchTier.DirectPrefixCanonical
                                    : (EF.Functions.ILike(row.Title, "%" + primary + "%")
                                        || (turkish != null && EF.Functions.ILike(row.Title, "%" + turkish + "%")))
                                        ? SearchBestMatchTier.DirectSubstringCanonical
                                        : row.NormalizedTitle.Length == folded.Length
                                          && EF.Functions.ILike(row.NormalizedTitle, folded)
                                            ? SearchBestMatchTier.FoldedExactCanonical
                                            : EF.Functions.ILike(row.NormalizedTitle, folded + "%")
                                                ? SearchBestMatchTier.FoldedPrefixCanonical
                                                : EF.Functions.ILike(row.NormalizedTitle, "%" + folded + "%")
                                                    ? SearchBestMatchTier.FoldedSubstringCanonical
                                                    : SearchBestMatchTier.NoMatch
                            : row.TitleKind == ContentSearchTitleKind.Original
                                ? (EF.Functions.ILike(row.Title, primary) && row.Title.Length == primary.Length)
                                  || (turkish != null
                                      && EF.Functions.ILike(row.Title, turkish)
                                      && row.Title.Length == turkish.Length)
                                    ? SearchBestMatchTier.DirectExactOriginal
                                    : (EF.Functions.ILike(row.Title, primary + "%")
                                        || (turkish != null && EF.Functions.ILike(row.Title, turkish + "%")))
                                        ? SearchBestMatchTier.DirectPrefixOriginal
                                        : (EF.Functions.ILike(row.Title, "%" + primary + "%")
                                            || (turkish != null && EF.Functions.ILike(row.Title, "%" + turkish + "%")))
                                            ? SearchBestMatchTier.DirectSubstringOriginal
                                            : row.NormalizedTitle.Length == folded.Length
                                              && EF.Functions.ILike(row.NormalizedTitle, folded)
                                                ? SearchBestMatchTier.FoldedExactOriginal
                                                : EF.Functions.ILike(row.NormalizedTitle, folded + "%")
                                                    ? SearchBestMatchTier.FoldedPrefixOriginal
                                                    : EF.Functions.ILike(row.NormalizedTitle, "%" + folded + "%")
                                                        ? SearchBestMatchTier.FoldedSubstringOriginal
                                                        : SearchBestMatchTier.NoMatch
                                : (EF.Functions.ILike(row.Title, primary) && row.Title.Length == primary.Length)
                                  || (turkish != null
                                      && EF.Functions.ILike(row.Title, turkish)
                                      && row.Title.Length == turkish.Length)
                                    ? SearchBestMatchTier.DirectExactAlias
                                    : (EF.Functions.ILike(row.Title, primary + "%")
                                        || (turkish != null && EF.Functions.ILike(row.Title, turkish + "%")))
                                        ? SearchBestMatchTier.DirectPrefixAlias
                                        : (EF.Functions.ILike(row.Title, "%" + primary + "%")
                                            || (turkish != null && EF.Functions.ILike(row.Title, "%" + turkish + "%")))
                                            ? SearchBestMatchTier.DirectSubstringAlias
                                            : row.NormalizedTitle.Length == folded.Length
                                              && EF.Functions.ILike(row.NormalizedTitle, folded)
                                                ? SearchBestMatchTier.FoldedExactAlias
                                                : EF.Functions.ILike(row.NormalizedTitle, folded + "%")
                                                    ? SearchBestMatchTier.FoldedPrefixAlias
                                                    : EF.Functions.ILike(row.NormalizedTitle, "%" + folded + "%")
                                                        ? SearchBestMatchTier.FoldedSubstringAlias
                                                        : SearchBestMatchTier.NoMatch))
                    .Min() ?? SearchBestMatchTier.NoMatch)
        });
    }
}
