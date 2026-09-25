namespace MovieApp.Application.Models.Catalog;

public readonly record struct MovieReleaseDateLookup(bool Exists, DateOnly? ReleaseDate);
