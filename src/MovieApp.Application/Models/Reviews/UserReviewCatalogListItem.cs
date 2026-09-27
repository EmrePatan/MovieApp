using MovieApp.Domain.Entities;

namespace MovieApp.Application.Models.Reviews;

public sealed record UserReviewCatalogListItem(Review Review, int? UserRating);
