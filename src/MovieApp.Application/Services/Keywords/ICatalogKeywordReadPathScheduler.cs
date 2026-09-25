namespace MovieApp.Application.Services.Keywords;

public interface ICatalogKeywordReadPathScheduler
{
    void ScheduleMovie(Guid movieId);

    void ScheduleTvShow(Guid tvShowId);
}
