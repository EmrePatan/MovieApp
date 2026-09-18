using MovieApp.Domain.Entities;

namespace MovieApp.Application.Models.Reviews;

public sealed record PublicReviewListItem(Review Review, int? UserRating);
