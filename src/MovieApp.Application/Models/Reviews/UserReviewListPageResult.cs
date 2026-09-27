using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Models.Reviews;

public sealed record UserReviewListPageResult(PaginatedResult<UserReviewListItemResult> Page);
