namespace MovieApp.Application.Services.CatalogShare;

public sealed record PublicWatchlistSharePageModel(
    string PageTitle,
    string PageDescription,
    string CanonicalPageUrl,
    string Heading,
    string ItemCountLabel,
    IReadOnlyList<PublicWebCatalogCard> Items,
    bool IsUnavailable);
