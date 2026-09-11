using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence;

internal static class TvShowStatusParser
{
    internal static TvShowStatus Parse(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "returning series" => TvShowStatus.ReturningSeries,
            "in production" => TvShowStatus.InProduction,
            "ended" => TvShowStatus.Ended,
            "canceled" or "cancelled" => TvShowStatus.Canceled,
            "pilot" => TvShowStatus.Pilot,
            _ => TvShowStatus.Planned
        };
}
