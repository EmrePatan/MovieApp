namespace MovieApp.Application.Models.WatchlistShare;

public sealed record WatchlistShareStatusResult(bool IsSharingEnabled);

public sealed record WatchlistShareEnableResult(string ShareUrl, bool CreatedNewLink);

public sealed record WatchlistShareRotateResult(string ShareUrl);
