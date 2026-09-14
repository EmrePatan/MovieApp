namespace MovieApp.Application.Services.MovieRelease;

public interface IMovieReleaseCheckService
{
    Task<MovieReleaseCheckResult> RunAsync(CancellationToken cancellationToken = default);
}
