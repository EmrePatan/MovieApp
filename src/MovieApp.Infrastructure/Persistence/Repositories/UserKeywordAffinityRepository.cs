using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Localization;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class UserKeywordAffinityRepository(ApplicationDbContext dbContext) : IUserKeywordAffinityRepository
{
    public async Task<IReadOnlyDictionary<Guid, int>> GetWatchedKeywordCountsAsync(
        Guid userId,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        var movieCounts = await dbContext.MovieKeywords.AsNoTracking()
            .Where(join => join.Keyword.ClassificationStatus != KeywordClassificationStatus.Excluded
                && join.Keyword.DisplayProfile != null
                && join.Keyword.DisplayProfile.Displayable
                && dbContext.WatchedMovies.Any(watched => watched.UserId == userId && watched.MovieId == join.MovieId))
            .GroupBy(join => join.KeywordId)
            .Select(group => new { KeywordId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var showCounts = await dbContext.TvShowKeywords.AsNoTracking()
            .Where(join => join.Keyword.ClassificationStatus != KeywordClassificationStatus.Excluded
                && join.Keyword.DisplayProfile != null
                && join.Keyword.DisplayProfile.Displayable
                && dbContext.WatchedEpisodes.Any(watched => watched.UserId == userId
                    && watched.Episode.Season.TvShowId == join.TvShowId))
            .GroupBy(join => join.KeywordId)
            .Select(group => new { KeywordId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return movieCounts.Concat(showCounts)
            .GroupBy(row => row.KeywordId)
            .Select(group => new KeyValuePair<Guid, int>(group.Key, group.Sum(row => row.Count)))
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .Take(maxCount)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetKeywordNamesAsync(
        IReadOnlyCollection<Guid> keywordIds,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (keywordIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var locale = KeywordDiscoverLocalizationSupport.NormalizeLocale(contentLocale);
        var english = SupportedContentLocales.EnglishUnitedStates;
        var rows = await dbContext.Keywords.AsNoTracking()
            .Where(keyword => keywordIds.Contains(keyword.Id))
            .Select(keyword => new
            {
                keyword.Id,
                keyword.Name,
                keyword.CanonicalName,
                Requested = keyword.Localizations
                    .Where(item => item.Locale == locale)
                    .Select(item => item.Name)
                    .FirstOrDefault(),
                English = keyword.Localizations
                    .Where(item => item.Locale == english)
                    .Select(item => item.Name)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => (row.Id, Name: KeywordDiscoverLocalizationSupport.ResolveDisplayName(
                row.Requested, row.English, row.CanonicalName, row.Name)))
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToDictionary(item => item.Id, item => item.Name);
    }
}
