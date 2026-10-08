using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Api.Identity;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Insights;

namespace MovieApp.UnitTests.Caching;

public sealed class PersonalizedCacheRebuilderTests
{
    [Fact]
    public async Task RebuildsTheMobileHomeRequestForBothLocalesAndIstanbulInsights()
    {
        var userId = Guid.NewGuid();
        var insights = new RecordingInsights();
        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
        services.AddScoped<RecordingHome>();
        services.AddScoped<IHomeService>(provider => provider.GetRequiredService<RecordingHome>());
        services.AddSingleton(insights);
        services.AddSingleton<IInsightsV3Service>(insights);
        services.AddSingleton<ILogger<PersonalizedCacheRebuilder>>(NullLogger<PersonalizedCacheRebuilder>.Instance);
        services.AddScoped<PersonalizedCacheRebuilder>();
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var rebuilder = scope.ServiceProvider.GetRequiredService<PersonalizedCacheRebuilder>();

        await rebuilder.RebuildAsync(userId, CancellationToken.None);

        var home = scope.ServiceProvider.GetRequiredService<RecordingHome>();

        Assert.Equal(
            PersonalizedCacheRebuilder.ContentLocales,
            home.Calls.Select(call => call.Locale).ToArray());
        Assert.All(home.Calls, call =>
        {
            Assert.Equal(PersonalizedCacheRebuilder.HomeSectionSize, call.Criteria.SectionSize);
            Assert.Equal(MovieApp.Application.Models.Search.SearchContentType.All, call.Criteria.Type);
            Assert.Equal(PersonalizedCacheRebuilder.ReleaseRegion, call.Region);
            Assert.Equal(userId, call.UserId);
        });
        Assert.Equal([(PersonalizedCacheRebuilder.InsightsTimeZoneId, (int?)null)], insights.Calls);
    }

    private sealed class RecordingHome(ICurrentUser currentUser) : IHomeService
    {
        public List<(HomeCriteria Criteria, string Locale, string? Region, Guid? UserId)> Calls { get; } = [];

        public Task<HomePersonalizedResult> GetHomePersonalizedAsync(
            HomeCriteria criteria,
            string contentLocale,
            string? releaseRegion = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((criteria, contentLocale, releaseRegion, currentUser.UserId));
            return Task.FromResult(new HomePersonalizedResult([], false, DateTime.UtcNow));
        }

        public Task<HomeResult> GetHomeAsync(
            HomeCriteria criteria,
            string contentLocale,
            string? releaseRegion = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<HomeBrowseResult> GetHomeBrowseAsync(
            HomeCriteria criteria,
            string contentLocale,
            string? releaseRegion = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingInsights : IInsightsV3Service
    {
        public List<(string TimeZoneId, int? Year)> Calls { get; } = [];

        public Task<InsightsV3Result> GetInsightsV3Async(
            string timeZoneId,
            int? year,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((timeZoneId, year));
            return Task.FromException<InsightsV3Result>(new InvalidOperationException("recorded"));
        }
    }
}
