namespace MovieApp.Application.Abstractions.Preferences;

public sealed record WatchPlatformPreferencesResult(IReadOnlyList<int> ProviderIds);

public interface IWatchPlatformPreferencesService
{
    Task<WatchPlatformPreferencesResult> GetAsync(CancellationToken cancellationToken = default);

    Task<WatchPlatformPreferencesResult> UpdateAsync(
        IReadOnlyList<int> providerIds, CancellationToken cancellationToken = default);
}
