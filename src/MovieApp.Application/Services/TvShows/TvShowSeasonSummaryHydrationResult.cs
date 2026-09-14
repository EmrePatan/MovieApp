using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.TvShows;

public sealed record TvShowSeasonSummaryHydrationResult(TvShow TvShow, bool ProviderCatalogRefreshed);
