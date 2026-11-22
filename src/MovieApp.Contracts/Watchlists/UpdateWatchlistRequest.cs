namespace MovieApp.Contracts.Watchlists;

/// <param name="Description">
/// Null leaves the description unchanged. An empty or whitespace value clears it.
/// </param>
public sealed record UpdateWatchlistRequest(string Name, string? Description = null);