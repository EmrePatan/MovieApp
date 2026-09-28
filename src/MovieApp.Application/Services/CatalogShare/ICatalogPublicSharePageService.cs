namespace MovieApp.Application.Services.CatalogShare;

public interface ICatalogPublicSharePageService
{
    Task<CatalogPublicSharePageModel?> TryGetMoviePageAsync(
        Guid movieId,
        string? contentLocale,
        CancellationToken cancellationToken = default);

    Task<CatalogPublicSharePageModel?> TryGetTvShowPageAsync(
        Guid tvShowId,
        string? contentLocale,
        CancellationToken cancellationToken = default);
}
