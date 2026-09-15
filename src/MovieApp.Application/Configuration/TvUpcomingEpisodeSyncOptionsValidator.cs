using Microsoft.Extensions.Options;

namespace MovieApp.Application.Configuration;

public sealed class TvUpcomingEpisodeSyncOptionsValidator : IValidateOptions<TvUpcomingEpisodeSyncOptions>
{
    public ValidateOptionsResult Validate(string? name, TvUpcomingEpisodeSyncOptions options)
    {
        if (options.BatchSize is < 1 or > 200)
        {
            return ValidateOptionsResult.Fail("TvUpcomingEpisodeSync:BatchSize must be between 1 and 200.");
        }

        if (options.FreshnessTtlHours is < 1 or > 168)
        {
            return ValidateOptionsResult.Fail("TvUpcomingEpisodeSync:FreshnessTtlHours must be between 1 and 168.");
        }

        return ValidateOptionsResult.Success;
    }
}
