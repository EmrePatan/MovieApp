namespace MovieApp.Contracts.WatchlistShare;

public sealed record WatchlistShareEnableRequest(Guid WatchlistId);

public sealed record WatchlistShareRotateRequest(Guid WatchlistId);
