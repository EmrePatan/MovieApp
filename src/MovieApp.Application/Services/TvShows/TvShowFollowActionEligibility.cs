using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.TvShows;

public static class TvShowFollowActionEligibility
{
    public static bool CanFollow(TvShowStatus status) =>
        status is not (TvShowStatus.Ended or TvShowStatus.Canceled);
}
