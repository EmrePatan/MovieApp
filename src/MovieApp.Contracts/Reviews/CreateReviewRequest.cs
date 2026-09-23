namespace MovieApp.Contracts.Reviews;

public sealed record CreateReviewRequest(string Content, string? AuthoringLocale = null);
