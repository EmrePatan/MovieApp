using MovieApp.Application.Configuration;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.StreamingLeaving;

/// <summary>Rules that decide when the service may be called.</summary>
public static class StreamingLeavingSchedule
{
    public static bool IsDue(StreamingLeavingRegion? state, StreamingAvailabilityOptions options, DateTime nowUtc)
    {
        if (state is null)
        {
            return true;
        }

        if (state.LastSuccessAtUtc is { } success && nowUtc - success < TimeSpan.FromDays(options.RefreshDays))
        {
            return false;
        }

        // An attempt newer than the last success failed or is still running.
        if (state.LastAttemptAtUtc is { } attempt &&
            (state.LastSuccessAtUtc is null || attempt > state.LastSuccessAtUtc))
        {
            return nowUtc - attempt >= TimeSpan.FromHours(options.FailureCooldownHours);
        }

        return true;
    }

    public static bool HasQuota(
        IReadOnlyDictionary<string, int> usageByRegion,
        string region,
        StreamingAvailabilityOptions options)
    {
        if (usageByRegion.Values.Sum() >= options.MonthlyRequestLimit)
        {
            return false;
        }

        if (IsReservedRegion(region, options))
        {
            return true;
        }

        var others = usageByRegion
            .Where(usage => !IsReservedRegion(usage.Key, options))
            .Sum(usage => usage.Value);
        return others < options.MonthlyRequestLimit - options.ReservedRegionRequests;
    }

    public static DateOnly PeriodStart(DateTime nowUtc, StreamingAvailabilityOptions options)
    {
        var resetDay = Math.Clamp(options.QuotaResetDay, 1, 28);
        var start = new DateOnly(nowUtc.Year, nowUtc.Month, resetDay);
        return nowUtc.Day >= resetDay ? start : start.AddMonths(-1);
    }

    private static bool IsReservedRegion(string region, StreamingAvailabilityOptions options) =>
        string.Equals(region, options.ReservedRegion, StringComparison.OrdinalIgnoreCase);
}
