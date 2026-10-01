using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;

namespace MovieApp.Infrastructure.Providers.Tmdb;

internal readonly record struct NewReleasesDiscoverConstraints(int MinVoteCount, int MaxAgeDays)
{
    public static NewReleasesDiscoverConstraints ForMovies(IOptions<NewReleasesOptions>? options)
    {
        var configured = options?.Value;
        return new NewReleasesDiscoverConstraints(
            Math.Max(0, configured?.MinVoteCountMovie ?? NewReleasesOptions.DefaultMinVoteCountMovie),
            Math.Max(0, configured?.MaxAgeDays ?? NewReleasesOptions.DefaultMaxAgeDays));
    }

    public static NewReleasesDiscoverConstraints ForTvShows(IOptions<NewReleasesOptions>? options)
    {
        var configured = options?.Value;
        return new NewReleasesDiscoverConstraints(
            Math.Max(0, configured?.MinVoteCountTv ?? NewReleasesOptions.DefaultMinVoteCountTv),
            Math.Max(0, configured?.MaxAgeDays ?? NewReleasesOptions.DefaultMaxAgeDays));
    }
}
