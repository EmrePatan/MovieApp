namespace MovieApp.Application.Models.TvShows;

public sealed record TvShowSearchRequest(string Query, int Page, int PageSize);
