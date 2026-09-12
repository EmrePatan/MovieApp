namespace MovieApp.Application.Configuration;

public sealed class SearchOptions
{
    public const string SectionName = "Search";

    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan ProviderRefreshInterval { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan ProviderRefreshLockDuration { get; set; } = TimeSpan.FromSeconds(30);

    public int MaxProviderDetailFetchesPerContentType { get; set; } = 20;

    public int MaxConcurrentProviderHttpRequests { get; set; } = 4;

    public TimeSpan ProviderRefreshLockRenewalInterval { get; set; } = TimeSpan.FromSeconds(10);
}
