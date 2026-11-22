using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MovieApp.Api.Controllers;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Recommendations;
using MovieApp.Contracts.Recommendations;

namespace MovieApp.UnitTests.Recommendations;

public sealed class RecommendationDismissEndpointTests
{
    [Fact]
    public async Task MovieDismissalCallsMovieServiceAndReturnsNoContent()
    {
        var service = new FakeDismissalService();
        var controller = CreateController(service);
        var movieId = Guid.NewGuid();

        var result = await controller.DismissRecommendation(
            new RecommendationDismissRequest("movie", movieId),
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(movieId, service.DismissedMovieId);
        Assert.Null(service.DismissedTvShowId);
    }

    [Fact]
    public async Task TvDismissalIsCaseInsensitiveAndCallsTvService()
    {
        var service = new FakeDismissalService();
        var controller = CreateController(service);
        var tvShowId = Guid.NewGuid();

        var result = await controller.DismissRecommendation(
            new RecommendationDismissRequest(" TV ", tvShowId),
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(tvShowId, service.DismissedTvShowId);
        Assert.Null(service.DismissedMovieId);
    }

    [Fact]
    public async Task UnknownContentTypeReturnsBadRequestWithoutCallingService()
    {
        var service = new FakeDismissalService();
        var controller = CreateController(service);

        var result = await controller.DismissRecommendation(
            new RecommendationDismissRequest("book", Guid.NewGuid()),
            CancellationToken.None);

        var badRequest = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Null(service.DismissedMovieId);
        Assert.Null(service.DismissedTvShowId);
    }

    [Fact]
    public async Task MissingTitleReturnsNotFound()
    {
        var service = new FakeDismissalService { ThrowNotFound = true };
        var controller = CreateController(service);

        var result = await controller.DismissRecommendation(
            new RecommendationDismissRequest("movie", Guid.NewGuid()),
            CancellationToken.None);

        var notFound = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    private static RecommendationsController CreateController(IRecommendationDismissalService service) =>
        // The dismissal actions never touch the recommendation query service.
        new(null!, service);

    private sealed class FakeDismissalService : IRecommendationDismissalService
    {
        public Guid? DismissedMovieId { get; private set; }

        public Guid? DismissedTvShowId { get; private set; }

        public bool ThrowNotFound { get; init; }

        public Task DismissMovieAsync(Guid movieId, CancellationToken cancellationToken = default)
        {
            if (ThrowNotFound)
            {
                throw new NotFoundException("The requested movie was not found.");
            }

            DismissedMovieId = movieId;
            return Task.CompletedTask;
        }

        public Task DismissTvShowAsync(Guid tvShowId, CancellationToken cancellationToken = default)
        {
            DismissedTvShowId = tvShowId;
            return Task.CompletedTask;
        }
    }
}
