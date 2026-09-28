using System.Globalization;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.WatchlistShare;

namespace MovieApp.Application.Services.CatalogShare;

public sealed class PublicWatchlistSharePageService(
    IWatchlistShareService watchlistShareService,
    IOptions<CatalogShareOptions> catalogShareOptions) : IPublicWatchlistSharePageService
{
    public async Task<PublicWatchlistSharePageModel> BuildPageAsync(
        string rawToken,
        string? acceptLanguageHeader,
        CancellationToken cancellationToken = default)
    {
        var trimmedToken = rawToken.Trim();
        var copy = WatchlistShareWebCopy.Resolve(acceptLanguageHeader);
        var canonical = WatchlistShareUrlBuilder.BuildCanonicalShareUrl(catalogShareOptions.Value, trimmedToken);
        if (!WatchlistShareTokenParser.IsValidPublicToken(trimmedToken))
        {
            return Unavailable(copy, canonical);
        }

        var publicData = await watchlistShareService.TryGetPublicByTokenAsync(trimmedToken, cancellationToken);

        if (publicData is null)
        {
            return Unavailable(copy, canonical);
        }

        var heading = WatchlistShareWebCopy.FormatPageHeading(
            publicData.WatchlistName,
            publicData.OwnerDisplayName,
            copy);
        var itemCountLabel = WatchlistShareWebCopy.FormatItemCount(publicData.Items.Count, copy);
        var description = string.Format(
            CultureInfo.InvariantCulture,
            copy.OgDescriptionMany,
            publicData.Items.Count);
        var options = catalogShareOptions.Value;

        var cards = publicData.Items
            .Select(item =>
            {
                var segment = item.ContentType == "movie" ? "movie" : "tv";
                return new PublicWebCatalogCard(
                    item.ContentId,
                    item.Title,
                    item.Year,
                    item.VoteAverage,
                    item.PosterPath,
                    CatalogShareWebUrls.BuildCanonicalUrl(options, segment, item.ContentId));
            })
            .ToList();

        return new PublicWatchlistSharePageModel(
            $"{heading} — Movie Cave",
            description,
            canonical,
            heading,
            itemCountLabel,
            cards,
            IsUnavailable: false);
    }

    private static PublicWatchlistSharePageModel Unavailable(
        WatchlistShareWebCopy.Copy copy,
        string canonical) =>
        new(
            $"{copy.NotSharedTitle} — Movie Cave",
            copy.NotSharedBody,
            canonical,
            copy.NotSharedTitle,
            string.Empty,
            [],
            IsUnavailable: true);
}
