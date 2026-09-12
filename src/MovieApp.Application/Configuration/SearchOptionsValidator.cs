using Microsoft.Extensions.Options;

namespace MovieApp.Application.Configuration;

public sealed class SearchOptionsValidator : IValidateOptions<SearchOptions>
{
    private static readonly TimeSpan MinimumDuration = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaximumCacheDuration = TimeSpan.FromDays(1);
    private static readonly TimeSpan MinimumProviderRefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaximumProviderRefreshInterval = TimeSpan.FromDays(7);
    private static readonly TimeSpan MinimumLockDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaximumLockDuration = TimeSpan.FromMinutes(5);

    public ValidateOptionsResult Validate(string? name, SearchOptions options)
    {
        var failures = new List<string>();

        ValidateDuration(options.CacheDuration, "Search:CacheDuration", MinimumDuration, MaximumCacheDuration, failures);
        ValidateDuration(
            options.ProviderRefreshInterval,
            "Search:ProviderRefreshInterval",
            MinimumProviderRefreshInterval,
            MaximumProviderRefreshInterval,
            failures);

        ValidateDuration(
            options.ProviderRefreshLockDuration,
            "Search:ProviderRefreshLockDuration",
            MinimumLockDuration,
            MaximumLockDuration,
            failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateDuration(
        TimeSpan duration,
        string propertyName,
        TimeSpan minimum,
        TimeSpan maximum,
        List<string> failures)
    {
        if (duration <= TimeSpan.Zero)
        {
            failures.Add($"{propertyName} must be greater than zero.");
            return;
        }

        if (duration < minimum)
        {
            failures.Add($"{propertyName} must be at least {minimum}.");
        }

        if (duration > maximum)
        {
            failures.Add($"{propertyName} must not exceed {maximum}.");
        }
    }
}
