namespace MovieApp.Application.Models.Search;

public sealed record SearchHistoryItem(Guid Id, string Query, DateTime SearchedAt);
