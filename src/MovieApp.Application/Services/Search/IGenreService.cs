namespace MovieApp.Application.Services.Search;

public interface IGenreService
{
    Task<IReadOnlyList<(Guid Id, string Name)>> GetAllAsync(
        string contentLocale,
        CancellationToken cancellationToken = default);
}
