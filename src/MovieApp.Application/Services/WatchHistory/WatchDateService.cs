using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.WatchHistory;

public enum WatchDateMode
{
    Day,
    Year,
    Release,
    Unknown,
}

public sealed record UpdateWatchDatesCommand(
    IReadOnlyList<Guid> MovieIds,
    IReadOnlyList<Guid> EpisodeIds,
    WatchDateMode Mode,
    DateOnly? Date,
    int? Year,
    Guid? TvShowId = null,
    bool? Undated = null,
    bool DryRun = false);

/// <param name="SkippedCount">Selected rows left unchanged: not watched, or no past release date.</param>
public sealed record UpdateWatchDatesResult(int UpdatedCount, int SkippedCount);

public interface IWatchDateService
{
    Task<UpdateWatchDatesResult> UpdateAsync(
        UpdateWatchDatesCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class WatchDateService(
    ICurrentUser currentUser,
    IWatchDateRepository repository,
    IUserAnalyticsCacheInvalidator analyticsCacheInvalidator,
    IServiceScopeFactory? analyticsScopeFactory = null) : IWatchDateService
{
    public const int MaxItems = 500;
    public const int MinYear = 1900;

    public async Task<UpdateWatchDatesResult> UpdateAsync(
        UpdateWatchDatesCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var utcNow = DateTime.UtcNow;
        var movieIds = command.MovieIds.Distinct().ToArray();
        var episodeIds = command.EpisodeIds.Distinct().ToArray();
        Validate(command, movieIds.Length + episodeIds.Length, utcNow);

        var updated = 0;
        var selected = movieIds.Length + episodeIds.Length;

        if (command.TvShowId is { } tvShowId)
        {
            var ofShow = await repository.GetWatchedEpisodesOfShowAsync(
                userId, tvShowId, command.Undated, cancellationToken);
            selected = ofShow.Count;

            foreach (var watched in ofShow)
            {
                if (Resolve(command, watched.WatchedAt, watched.Episode?.AirDate, utcNow) is { } value)
                {
                    if (!command.DryRun)
                    {
                        watched.SetWatchedDate(value.WatchedAt, value.Precision, utcNow);
                    }

                    updated++;
                }
            }
        }

        if (movieIds.Length > 0)
        {
            foreach (var watched in await repository.GetWatchedMoviesAsync(userId, movieIds, cancellationToken))
            {
                if (Resolve(command, watched.WatchedAt, watched.Movie?.ReleaseDate, utcNow) is { } value)
                {
                    if (!command.DryRun)
                    {
                        watched.SetWatchedDate(value.WatchedAt, value.Precision, utcNow);
                    }

                    updated++;
                }
            }
        }

        if (episodeIds.Length > 0)
        {
            foreach (var watched in await repository.GetWatchedEpisodesAsync(userId, episodeIds, cancellationToken))
            {
                if (Resolve(command, watched.WatchedAt, watched.Episode?.AirDate, utcNow) is { } value)
                {
                    if (!command.DryRun)
                    {
                        watched.SetWatchedDate(value.WatchedAt, value.Precision, utcNow);
                    }

                    updated++;
                }
            }
        }

        if (updated > 0 && !command.DryRun)
        {
            await repository.SaveChangesAsync(cancellationToken);
            await BackgroundAnalyticsInvalidation.RunAsync(
                analyticsCacheInvalidator,
                analyticsScopeFactory,
                userId,
                cancellationToken);
        }

        return new UpdateWatchDatesResult(updated, selected - updated);
    }

    private static void Validate(UpdateWatchDatesCommand command, int itemCount, DateTime utcNow)
    {
        if (command.TvShowId is not null)
        {
            // A whole show has no item limit, but must not be mixed with a hand-picked selection.
            if (itemCount > 0)
            {
                throw new ValidationException("Send either a TV show or individual items, not both.");
            }
        }
        else if (itemCount is 0 or > MaxItems)
        {
            throw new ValidationException($"Select between 1 and {MaxItems} items.");
        }

        // One day of slack: "today" in the user's time zone can already be tomorrow in UTC.
        var latestDay = DateOnly.FromDateTime(utcNow).AddDays(1);
        if (command.Mode == WatchDateMode.Day
            && (command.Date is not { } date || date.Year < MinYear || date > latestDay))
        {
            throw new ValidationException("A past date is required.");
        }

        if (command.Mode == WatchDateMode.Year
            && (command.Year is not { } year || year < MinYear || year > latestDay.Year))
        {
            throw new ValidationException("A past year is required.");
        }
    }

    /// <summary>Null means "leave this row unchanged".</summary>
    internal static (DateTime WatchedAt, WatchDatePrecision Precision)? Resolve(
        UpdateWatchDatesCommand command,
        DateTime currentWatchedAt,
        DateOnly? releaseDate,
        DateTime utcNow)
    {
        switch (command.Mode)
        {
            case WatchDateMode.Day:
                return (AtNoonNotAfter(command.Date!.Value, utcNow), WatchDatePrecision.Day);
            case WatchDateMode.Year:
                // Mid-year keeps the value inside the chosen year in every time zone.
                return (AtNoonNotAfter(new DateOnly(command.Year!.Value, 7, 1), utcNow), WatchDatePrecision.Year);
            case WatchDateMode.Release:
                return releaseDate is { } released && released <= DateOnly.FromDateTime(utcNow)
                    ? (AtNoonNotAfter(released, utcNow), WatchDatePrecision.Day)
                    : null;
            case WatchDateMode.Unknown:
                // The old timestamp is kept so non-statistical ordering stays stable.
                return (currentWatchedAt, WatchDatePrecision.Unknown);
            default:
                throw new ValidationException("Unknown date mode.");
        }
    }

    // Noon UTC keeps the same calendar day for offsets up to 12 hours.
    private static DateTime AtNoonNotAfter(DateOnly date, DateTime utcNow)
    {
        var noon = date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
        return noon > utcNow ? utcNow : noon;
    }
}
