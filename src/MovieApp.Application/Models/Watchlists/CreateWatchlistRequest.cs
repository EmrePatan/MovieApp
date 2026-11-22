namespace MovieApp.Application.Models.Watchlists;

/// <param name="Description">Optional. Blank or null means no description.</param>
public sealed record CreateWatchlistRequest(string Name, string? Description = null);