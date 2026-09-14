using MovieApp.Application.Services.ReleaseDetection;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Domain.Notifications;

namespace MovieApp.UnitTests.ReleaseDetection;

public sealed class ReleaseDetectionScannerTests
{
    private static readonly Guid TvShowId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly DateOnly Today = new(2026, 9, 15);
    private static readonly DateOnly Future = new(2026, 9, 20);
    private static readonly DateOnly Past = new(2026, 9, 1);

    [Fact]
    public void FutureEpisode_DoesNotCreateEvent()
    {
        var seasons = new[]
        {
            CreateSeason(1, episodes: CreateEpisode(5, Future))
        };

        var events = Detect(seasons, Today);

        Assert.Empty(events);
    }

    [Fact]
    public void KnownFutureEpisodeCrossesBoundary_CreatesOneEpisodeEvent()
    {
        var seasons = new[]
        {
            CreateSeason(1, episodes: CreateEpisode(5, Today))
        };

        var events = Detect(seasons, Today);

        var episodeEvent = Assert.Single(events, eventItem => eventItem.EventType == CatalogReleaseEventType.NewEpisode);
        Assert.Equal(CatalogReleaseEventType.NewEpisode, episodeEvent.EventType);
        Assert.Equal(1, episodeEvent.SeasonNumber);
        Assert.Equal(5, episodeEvent.EpisodeNumber);
        Assert.Equal(ReleaseDateTime.ToReleaseAtUtc(Today), episodeEvent.ReleaseAtUtc);
    }

    [Fact]
    public void AlreadyReleasedEpisodeWithNoEvent_CreatesEvent()
    {
        var seasons = new[]
        {
            CreateSeason(1, episodes: CreateEpisode(1, Past))
        };

        var events = Detect(seasons, Today);

        Assert.Contains(events, eventItem => eventItem.EventType == CatalogReleaseEventType.NewEpisode);
    }

    [Fact]
    public void RepeatedScan_DoesNotCreateDuplicate()
    {
        var seasons = new[]
        {
            CreateSeason(1, episodes: CreateEpisode(1, Past))
        };

        var firstScan = Detect(seasons, Today);
        var dedupeKeys = firstScan.Select(eventItem => eventItem.DedupeKey).ToHashSet(StringComparer.Ordinal);
        var secondScan = Detect(seasons, Today, dedupeKeys);

        Assert.NotEmpty(firstScan);
        Assert.Empty(secondScan);
    }

    [Fact]
    public void NullAirDate_DoesNotCreateEvent()
    {
        var seasons = new[]
        {
            CreateSeason(1, episodes: CreateEpisode(1, null))
        };

        var events = Detect(seasons, Today);

        Assert.Empty(events);
    }

    [Fact]
    public void SeasonZeroEpisode_DoesNotCreateEvent()
    {
        var seasons = new[]
        {
            CreateSeason(0, episodes: CreateEpisode(1, Past))
        };

        var events = Detect(seasons, Today);

        Assert.Empty(events);
    }

    [Fact]
    public void RegularSeasonWithFirstAiredEpisode_CreatesPremiereEvent()
    {
        var seasons = new[]
        {
            CreateSeason(2,
                episodes:
                [
                    CreateEpisode(1, Past)[0],
                    CreateEpisode(2, Today)[0]
                ])
        };

        var events = Detect(seasons, Today);

        var premiereEvent = Assert.Single(events, eventItem => eventItem.EventType == CatalogReleaseEventType.NewSeasonPremiere);
        Assert.Equal(2, premiereEvent.SeasonNumber);
        Assert.Equal(ReleaseDateTime.ToReleaseAtUtc(Past), premiereEvent.ReleaseAtUtc);
    }

    [Fact]
    public void SeasonWithOnlyFutureEpisodes_DoesNotCreatePremiereEvent()
    {
        var seasons = new[]
        {
            CreateSeason(2, episodes: CreateEpisode(1, Future))
        };

        var events = Detect(seasons, Today);

        Assert.DoesNotContain(events, eventItem => eventItem.EventType == CatalogReleaseEventType.NewSeasonPremiere);
    }

    [Fact]
    public void SeasonSummaryFallbackWithEpisodeCountAndPastAirDate_CreatesPremiereEvent()
    {
        var seasons = new[]
        {
            CreateSeason(3, airDate: Past, episodeCount: 8)
        };

        var events = Detect(seasons, Today);

        var premiereEvent = Assert.Single(events);
        Assert.Equal(CatalogReleaseEventType.NewSeasonPremiere, premiereEvent.EventType);
        Assert.Equal(3, premiereEvent.SeasonNumber);
    }

    [Fact]
    public void FutureSeasonAirDate_DoesNotCreatePremiereEvent()
    {
        var seasons = new[]
        {
            CreateSeason(3, airDate: Future, episodeCount: 8)
        };

        var events = Detect(seasons, Today);

        Assert.Empty(events);
    }

    [Fact]
    public void SeasonZero_DoesNotCreatePremiereEvent()
    {
        var seasons = new[]
        {
            CreateSeason(0, airDate: Past, episodeCount: 5, episodes: CreateEpisode(1, Past))
        };

        var events = Detect(seasons, Today);

        Assert.Empty(events);
    }

    [Fact]
    public void BaselineAbsorb_UsesBaselineSource()
    {
        var seasons = new[]
        {
            CreateSeason(1, episodes: CreateEpisode(1, Past))
        };

        var events = Detect(seasons, Today, source: CatalogReleaseEventSource.BaselineAbsorb);

        Assert.All(events, eventItem => Assert.Equal(CatalogReleaseEventSource.BaselineAbsorb, eventItem.Source));
    }

    [Fact]
    public void AirDateEdit_FutureToToday_CreatesEvent()
    {
        var futureSeasons = new[] { CreateSeason(1, episodes: CreateEpisode(1, Future)) };
        Assert.Empty(Detect(futureSeasons, Today));

        var todaySeasons = new[] { CreateSeason(1, episodes: CreateEpisode(1, Today)) };
        Assert.Contains(Detect(todaySeasons, Today), eventItem => eventItem.EventType == CatalogReleaseEventType.NewEpisode);
    }

    [Fact]
    public void AirDateEdit_FutureToPast_CreatesEvent()
    {
        var futureSeasons = new[] { CreateSeason(1, episodes: CreateEpisode(1, Future)) };
        Assert.Empty(Detect(futureSeasons, Today));

        var pastSeasons = new[] { CreateSeason(1, episodes: CreateEpisode(1, Past)) };
        Assert.Contains(Detect(pastSeasons, Today), eventItem => eventItem.EventType == CatalogReleaseEventType.NewEpisode);
    }

    [Fact]
    public void AirDateEdit_PastToDifferentPast_DoesNotCreateDuplicate()
    {
        var originalPast = new DateOnly(2026, 8, 1);
        var revisedPast = new DateOnly(2026, 8, 10);

        var firstSeasons = new[] { CreateSeason(1, episodes: CreateEpisode(1, originalPast)) };
        var firstScan = Detect(firstSeasons, Today);
        Assert.Contains(firstScan, eventItem => eventItem.EventType == CatalogReleaseEventType.NewEpisode);

        var dedupeKeys = firstScan.Select(eventItem => eventItem.DedupeKey).ToHashSet(StringComparer.Ordinal);
        var revisedSeasons = new[] { CreateSeason(1, episodes: CreateEpisode(1, revisedPast)) };
        var secondScan = Detect(revisedSeasons, Today, dedupeKeys);

        Assert.Empty(secondScan);
    }

    [Fact]
    public void AirDateEdit_PastToFutureAfterEvent_DoesNotRetractEvent()
    {
        var dedupeKey = CatalogReleaseEventDedupeKey.ForEpisode(TvShowId, 1, 1);
        var futureSeasons = new[] { CreateSeason(1, episodes: CreateEpisode(1, Future)) };

        var events = Detect(futureSeasons, Today, new HashSet<string>([dedupeKey], StringComparer.Ordinal));

        Assert.Empty(events);
    }

    [Fact]
    public void AirDateEdit_ReleasedToNullAfterEvent_DoesNotRetractEvent()
    {
        var dedupeKey = CatalogReleaseEventDedupeKey.ForEpisode(TvShowId, 1, 1);
        var nullSeasons = new[] { CreateSeason(1, episodes: CreateEpisode(1, null)) };

        var events = Detect(nullSeasons, Today, new HashSet<string>([dedupeKey], StringComparer.Ordinal));

        Assert.Empty(events);
    }

    [Fact]
    public void AirDateEdit_FutureToNullBeforeRelease_DoesNotCreateEvent()
    {
        var seasons = new[]
        {
            CreateSeason(1, episodes: CreateEpisode(1, null))
        };

        var events = Detect(seasons, Today);

        Assert.Empty(events);
    }

    [Theory]
    [InlineData(CatalogReleaseEventSource.BoundaryDetection)]
    [InlineData(CatalogReleaseEventSource.ProviderRefresh)]
    public void BoundaryModes_CreateSameFactualEvents(CatalogReleaseEventSource source)
    {
        var seasons = new[]
        {
            CreateSeason(1, episodes: CreateEpisode(1, Past))
        };

        var events = Detect(seasons, Today, source: source);

        Assert.Contains(events, eventItem => eventItem.EventType == CatalogReleaseEventType.NewEpisode);
        Assert.All(events, eventItem => Assert.Equal(source, eventItem.Source));
    }

    private static IReadOnlyList<CatalogReleaseEvent> Detect(
        IReadOnlyList<Season> seasons,
        DateOnly boundary,
        IReadOnlySet<string>? existingDedupeKeys = null,
        CatalogReleaseEventSource source = CatalogReleaseEventSource.BoundaryDetection) =>
        ReleaseDetectionScanner.DetectMissingEvents(
            TvShowId,
            seasons,
            boundary,
            existingDedupeKeys ?? new HashSet<string>(StringComparer.Ordinal),
            source,
            DateTime.UtcNow);

    private static Season CreateSeason(
        int seasonNumber,
        DateOnly? airDate = null,
        int? episodeCount = null,
        params Episode[] episodes) =>
        new()
        {
            Id = Guid.NewGuid(),
            TvShowId = TvShowId,
            SeasonNumber = seasonNumber,
            AirDate = airDate,
            EpisodeCount = episodeCount,
            Episodes = episodes.ToList(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    private static Episode[] CreateEpisode(int episodeNumber, DateOnly? airDate) =>
    [
        new()
        {
            Id = Guid.NewGuid(),
            EpisodeNumber = episodeNumber,
            AirDate = airDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }
    ];
}
