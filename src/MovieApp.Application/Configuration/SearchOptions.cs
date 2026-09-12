namespace MovieApp.Application.Configuration;

public sealed class SearchOptions
{
    public const string SectionName = "Search";

    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan ProviderRefreshInterval { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan ProviderRefreshLockDuration { get; set; } = TimeSpan.FromSeconds(30);
}
