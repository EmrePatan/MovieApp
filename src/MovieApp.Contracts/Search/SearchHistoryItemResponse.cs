namespace MovieApp.Contracts.Search;

public sealed record SearchHistoryItemResponse(Guid Id, string Query, DateTime SearchedAt);
