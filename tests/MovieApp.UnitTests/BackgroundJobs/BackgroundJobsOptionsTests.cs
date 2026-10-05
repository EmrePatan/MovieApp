using Microsoft.Extensions.Configuration;
using MovieApp.Api.BackgroundJobs;
using MovieApp.Application.Configuration;

namespace MovieApp.UnitTests.BackgroundJobs;

public sealed class BackgroundJobsOptionsTests
{
    [Fact]
    public void DefaultCronValuesUseStaggeredProductionSchedule()
    {
        var options = new BackgroundJobsOptions();

        Assert.Equal("5 */6 * * *", options.TmdbMovieChangesCron);
        Assert.Equal("20 */6 * * *", options.TmdbTvChangesCron);
        Assert.Equal("10 * * * *", options.HotReleaseCron);
        Assert.Equal("25 * * * *", options.MovieReleaseCron);
    }

    [Fact]
    public void BindsCronValuesFromConfigurationOverDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundJobs:TmdbMovieChangesCron"] = "7 */6 * * *",
                ["BackgroundJobs:TmdbTvChangesCron"] = "22 */6 * * *",
                ["BackgroundJobs:HotReleaseCron"] = "12 * * * *",
                ["BackgroundJobs:MovieReleaseCron"] = "27 * * * *",
            })
            .Build();

        var options = configuration
            .GetSection(BackgroundJobsOptions.SectionName)
            .Get<BackgroundJobsOptions>();

        Assert.NotNull(options);
        Assert.Equal("7 */6 * * *", options.TmdbMovieChangesCron);
        Assert.Equal("22 */6 * * *", options.TmdbTvChangesCron);
        Assert.Equal("12 * * * *", options.HotReleaseCron);
        Assert.Equal("27 * * * *", options.MovieReleaseCron);
    }

    [Fact]
    public void ValidatorRejectsEmptyCron()
    {
        var validator = new BackgroundJobsOptionsValidator();
        var result = validator.Validate(
            null,
            new BackgroundJobsOptions { HotReleaseCron = "   " });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("HotReleaseCron", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidatorAcceptsProductionStaggeredCrons()
    {
        var validator = new BackgroundJobsOptionsValidator();
        var result = validator.Validate(
            null,
            new BackgroundJobsOptions
            {
                TmdbMovieChangesCron = "5 */6 * * *",
                TmdbTvChangesCron = "20 */6 * * *",
                HotReleaseCron = "10 * * * *",
                MovieReleaseCron = "25 * * * *",
            });

        Assert.True(result.Succeeded);
    }
}
