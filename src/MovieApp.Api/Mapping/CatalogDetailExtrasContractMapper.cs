using MovieApp.Application.Models.Catalog;
using MovieApp.Contracts.Catalog;

namespace MovieApp.Api.Mapping;

public static class CatalogDetailExtrasContractMapper
{
    public static CatalogDetailExtrasResponse? ToResponse(CatalogDetailExtras? extras) =>
        extras is null
            ? null
            : new CatalogDetailExtrasResponse(
                extras.Tagline,
                extras.Certifications,
                extras.Networks,
                extras.Creators,
                extras.ProductionCompanies,
                extras.Budget,
                extras.Revenue,
                extras.LastEpisode is { } episode
                    ? new CatalogLastEpisodeResponse(episode.SeasonNumber, episode.EpisodeNumber, episode.AirDate)
                    : null);
}
