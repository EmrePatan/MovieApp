using Microsoft.Extensions.Configuration;
using MovieApp.Application.Configuration;

namespace MovieApp.UnitTests.Home;

public sealed class HomeOptionsTests
{
    [Fact]
    public void DefaultComingUpSectionSize_IsTen()
    {
        var options = new HomeOptions();

        Assert.Equal(10, options.ComingUpSectionSize);
    }

    [Fact]
    public void BindsComingUpSectionSizeFromConfigurationOverDefault()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Home:ComingUpSectionSize"] = "7",
            })
            .Build();

        var options = configuration
            .GetSection(HomeOptions.SectionName)
            .Get<HomeOptions>();

        Assert.NotNull(options);
        Assert.Equal(7, options.ComingUpSectionSize);
    }
}
