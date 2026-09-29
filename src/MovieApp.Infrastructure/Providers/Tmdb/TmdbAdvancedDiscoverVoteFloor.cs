using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;

namespace MovieApp.Infrastructure.Providers.Tmdb;

internal static class TmdbAdvancedDiscoverVoteFloor
{
    public static int Resolve(IOptions<AdvancedDiscoverOptions>? options) =>
        options?.Value.MinVoteCountWhenWatchProvider
        ?? AdvancedDiscoverOptions.DefaultMinVoteCountWhenWatchProvider;
}
