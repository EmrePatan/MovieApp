using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;

namespace MovieApp.Api.BackgroundJobs;

public sealed class BackgroundJobsOptionsValidator : IValidateOptions<BackgroundJobsOptions>
{
    public ValidateOptionsResult Validate(string? name, BackgroundJobsOptions options)
    {
        var failures = new List<string>();

        ValidateCronNotEmpty(options.TmdbMovieChangesCron, nameof(BackgroundJobsOptions.TmdbMovieChangesCron), failures);
        ValidateCronNotEmpty(options.TmdbTvChangesCron, nameof(BackgroundJobsOptions.TmdbTvChangesCron), failures);
        ValidateCronNotEmpty(options.HotReleaseCron, nameof(BackgroundJobsOptions.HotReleaseCron), failures);
        ValidateCronNotEmpty(options.MovieReleaseCron, nameof(BackgroundJobsOptions.MovieReleaseCron), failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateCronNotEmpty(string? cron, string propertyName, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(cron))
        {
            failures.Add($"BackgroundJobs:{propertyName} must not be empty.");
        }
    }
}
