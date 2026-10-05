using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Services.Library;

namespace MovieApp.UnitTests.Library;

public sealed class LibraryActionStatusServiceTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ContentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task GetAsyncRejectsEpisodeIdForMovie()
    {
        var service = new LibraryActionStatusService(
            new AuthenticatedCurrentUser(UserId),
            new NoOpLibraryActionStatusRepository());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.GetAsync("movie", ContentId, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetAsyncDispatchesMovieRepositoryCall()
    {
        var repository = new RecordingLibraryActionStatusRepository();
        var service = new LibraryActionStatusService(new AuthenticatedCurrentUser(UserId), repository);

        _ = await service.GetAsync("movie", ContentId, null);

        Assert.True(repository.MovieCalled);
        Assert.Equal(UserId, repository.MovieUserId);
        Assert.Equal(ContentId, repository.MovieId);
    }

    private sealed class AuthenticatedCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class NoOpLibraryActionStatusRepository : ILibraryActionStatusRepository
    {
        public Task<LibraryActionSnapshot> GetMovieAsync(
            Guid userId,
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<LibraryActionSnapshot> GetTvShowAsync(
            Guid userId,
            Guid tvShowId,
            Guid? episodeId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class RecordingLibraryActionStatusRepository : ILibraryActionStatusRepository
    {
        public bool MovieCalled { get; private set; }

        public Guid MovieUserId { get; private set; }

        public Guid MovieId { get; private set; }

        public Task<LibraryActionSnapshot> GetMovieAsync(
            Guid userId,
            Guid movieId,
            CancellationToken cancellationToken = default)
        {
            MovieCalled = true;
            MovieUserId = userId;
            MovieId = movieId;
            return Task.FromResult(new LibraryActionSnapshot(
                "movie",
                movieId,
                false,
                false,
                [],
                false,
                false,
                false,
                false,
                false,
                null));
        }

        public Task<LibraryActionSnapshot> GetTvShowAsync(
            Guid userId,
            Guid tvShowId,
            Guid? episodeId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
