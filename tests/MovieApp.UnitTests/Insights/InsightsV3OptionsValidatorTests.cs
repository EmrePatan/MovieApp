using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;

namespace MovieApp.UnitTests.Insights;

public sealed class InsightsV3OptionsValidatorTests
{
    private readonly InsightsV3OptionsValidator _validator = new();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(11)]
    public void ValidateAcceptsValuesBetween1And11(int maxConcurrency)
    {
        var result = _validator.Validate(
            null,
            new InsightsV3Options { MaxRepositoryConcurrency = maxConcurrency });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(12)]
    [InlineData(100)]
    public void ValidateRejectsOutOfRangeValues(int maxConcurrency)
    {
        var result = _validator.Validate(
            null,
            new InsightsV3Options { MaxRepositoryConcurrency = maxConcurrency });

        Assert.False(result.Succeeded);
        Assert.Contains("MaxRepositoryConcurrency", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultOptionsUseBoundedConcurrency()
    {
        Assert.Equal(InsightsV3Options.DefaultMaxRepositoryConcurrency, new InsightsV3Options().MaxRepositoryConcurrency);
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    [InlineData("11", 11)]
    public void ConfigurationBindingResolvesMaxRepositoryConcurrency(string? configuredValue, int expected)
    {
        var configurationData = new Dictionary<string, string?>();
        if (configuredValue is not null)
        {
            configurationData[$"{InsightsV3Options.SectionName}:MaxRepositoryConcurrency"] = configuredValue;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var options = configuration.GetSection(InsightsV3Options.SectionName).Get<InsightsV3Options>()
            ?? new InsightsV3Options();

        Assert.Equal(expected, options.MaxRepositoryConcurrency);
    }
}
