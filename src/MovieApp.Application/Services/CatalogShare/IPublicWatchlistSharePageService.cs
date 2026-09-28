namespace MovieApp.Application.Services.CatalogShare;

public interface IPublicWatchlistSharePageService
{
    Task<PublicWatchlistSharePageModel> BuildPageAsync(
        string rawToken,
        string? acceptLanguageHeader,
        CancellationToken cancellationToken = default);
}
