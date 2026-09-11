namespace MovieApp.Application.Models.Movies;

public sealed record MovieSearchRequest(string Query, int Page, int PageSize);
