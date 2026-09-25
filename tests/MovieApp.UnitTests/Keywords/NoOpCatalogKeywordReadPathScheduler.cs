using MovieApp.Application.Services.Keywords;

namespace MovieApp.UnitTests.Keywords;

internal sealed class NoOpCatalogKeywordReadPathScheduler : ICatalogKeywordReadPathScheduler
{
    public void ScheduleMovie(Guid movieId)
    {
    }

    public void ScheduleTvShow(Guid tvShowId)
    {
    }
}
