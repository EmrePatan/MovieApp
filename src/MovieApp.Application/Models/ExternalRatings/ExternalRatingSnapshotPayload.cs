namespace MovieApp.Application.Models.ExternalRatings;

public sealed class ExternalRatingSnapshotPayload
{
    public List<ExternalRatingItem> Ratings { get; set; } = [];

    public bool IsNegative { get; set; }
}
