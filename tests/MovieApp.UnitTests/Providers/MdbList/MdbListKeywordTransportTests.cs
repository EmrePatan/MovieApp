using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Models.Keywords;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Providers.MdbList;
using MovieApp.UnitTests.Providers.Tmdb;

namespace MovieApp.UnitTests.Providers.MdbList;

public sealed class MdbListKeywordTransportTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void RatingsOnlyResponseDeserializesUnchanged()
    {
        const string json = """
            {
              "title": "Jaws",
              "ratings": [
                { "source": "imdb", "value": 8.1, "votes": 673852 },
                { "source": "tmdb", "value": 76, "votes": 10114 }
              ]
            }
            """;

        var payload = JsonSerializer.Deserialize<MdbListTitleResponseJson>(json, SerializerOptions);

        Assert.NotNull(payload);
        Assert.Equal(2, payload!.Ratings!.Count);
        Assert.Null(payload.Keywords);
    }

    [Fact]
    public void KeywordAppendResponseDeserializesExternalIdAndName()
    {
        const string json = """
            {
              "ratings": [],
              "keywords": [
                { "id": 96, "name": "monk" },
                { "id": 6325, "name": "convent-nunnery" }
              ]
            }
            """;

        var payload = JsonSerializer.Deserialize<MdbListTitleResponseJson>(json, SerializerOptions);
        var mapped = MdbListKeywordTransportMapper.MapKeywords(payload!.Keywords);

        Assert.Equal(2, mapped.Count);
        Assert.Equal(KeywordProvider.MdbList, mapped[0].Provider);
        Assert.Equal(96, mapped[0].ExternalId);
        Assert.Equal("monk", mapped[0].Name);
        Assert.Equal(6325, mapped[1].ExternalId);
    }

    [Fact]
    public void MissingKeywordArrayProducesEmptyCollection()
    {
        var fromNull = MdbListKeywordTransportMapper.MapKeywords(null);
        var fromEmpty = MdbListKeywordTransportMapper.MapKeywords([]);

        Assert.Empty(fromNull);
        Assert.Empty(fromEmpty);
    }

    [Theory]
    [InlineData("movie", CatalogContentType.Movie, "movie")]
    [InlineData("show", CatalogContentType.Tv, "show")]
    public async Task KeywordFetchUsesCorrectMediaSegment(
        string expectedSegment,
        CatalogContentType mediaType,
        string segmentInResponse)
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ratings":[],"keywords":[]}""")
        });

        var apiClient = CreateApiClient(handler);
        var provider = new MdbListKeywordTransportProvider(apiClient);

        if (mediaType == CatalogContentType.Movie)
        {
            await provider.FetchKeywordsAsync(CatalogContentType.Movie, 578);
        }
        else
        {
            await provider.FetchKeywordsAsync(CatalogContentType.Tv, 1399);
        }

        var requestPath = handler.Requests.Single().RequestUri!.PathAndQuery;
        Assert.Contains($"tmdb/{expectedSegment}/", requestPath, StringComparison.Ordinal);
        Assert.Contains("append_to_response=keyword", requestPath, StringComparison.Ordinal);
        Assert.DoesNotContain(segmentInResponse == "movie" ? "/show/" : "/movie/", requestPath, StringComparison.Ordinal);
    }

    [Fact]
    public void KeywordTransportReturnsProviderScopedTransportItems()
    {
        var items = MdbListKeywordTransportMapper.MapKeywords(
        [
            new MdbListKeywordJson { Id = 1, Name = "test" }
        ]);

        Assert.All(items, item => Assert.IsType<MdbListKeywordTransportItem>(item));
        Assert.All(items, item => Assert.Equal(KeywordProvider.MdbList, item.Provider));
    }

    [Fact]
    public void MdbListExternalIdIsNotAssumedToEqualTmdbKeywordId()
    {
        const int mdbListKeywordId = 304495;
        const int tmdbKeywordId = 1305;

        var mapped = MdbListKeywordTransportMapper.MapKeywords(
        [
            new MdbListKeywordJson { Id = mdbListKeywordId, Name = "welfare" },
            new MdbListKeywordJson { Id = tmdbKeywordId, Name = "mother" },
        ]);

        Assert.Contains(mapped, item => item.ExternalId == mdbListKeywordId && item.Name == "welfare");
        Assert.Contains(mapped, item => item.ExternalId == tmdbKeywordId && item.Name == "mother");
        Assert.DoesNotContain(mapped, item => item.ExternalId == mdbListKeywordId && item.Name == "mother");
    }

    [Fact]
    public void MalformedKeywordItemsAreSkipped()
    {
        var mapped = MdbListKeywordTransportMapper.MapKeywords(
        [
            new MdbListKeywordJson { Id = 0, Name = "invalid-id" },
            new MdbListKeywordJson { Id = -1, Name = "negative" },
            new MdbListKeywordJson { Id = 10, Name = null },
            new MdbListKeywordJson { Id = 11, Name = "   " },
            new MdbListKeywordJson { Id = 12, Name = "valid" },
        ]);

        Assert.Single(mapped);
        Assert.Equal(12, mapped[0].ExternalId);
        Assert.Equal("valid", mapped[0].Name);
    }

    [Fact]
    public async Task ExternalRatingsProviderStillRequestsWithoutKeywordAppend()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "ratings": [
                    { "source": "imdb", "value": 8.1, "votes": 1 },
                    { "source": "tmdb", "value": 7.6, "votes": 2 }
                  ]
                }
                """)
        });

        var apiClient = CreateApiClient(handler);
        var ratingsProvider = new MdbListExternalRatingsProvider(apiClient);
        var result = await ratingsProvider.FetchAsync(CatalogContentType.Movie, 578);

        Assert.NotNull(result);
        Assert.Single(result!.Payload.Ratings);
        Assert.Equal("imdb", result.Payload.Ratings[0].Source);

        var requestPath = handler.Requests.Single().RequestUri!.PathAndQuery;
        Assert.DoesNotContain("append_to_response", requestPath, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildTmdbTitleRelativePathEncodesAppendToResponse()
    {
        var path = MdbListApiClient.BuildTmdbTitleRelativePath(
            "movie",
            578,
            "secret-key",
            ["keyword"]);

        Assert.Equal(
            "tmdb/movie/578?apikey=secret-key&append_to_response=keyword",
            path);
    }

    [Fact]
    public async Task BatchKeywordFetchUsesOfficialPostContract()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            var path = request.RequestUri!.PathAndQuery;
            Assert.Contains("tmdb/movie/", path, StringComparison.Ordinal);
            Assert.Contains("apikey=", path, StringComparison.Ordinal);
            Assert.DoesNotContain("append_to_response=", path, StringComparison.Ordinal);

            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            using var document = JsonDocument.Parse(body);
            Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("append_to_response").ValueKind);
            Assert.Equal("keyword", document.RootElement.GetProperty("append_to_response")[0].GetString());
            Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("ids").ValueKind);
            Assert.Equal("578", document.RootElement.GetProperty("ids")[0].GetString());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    [
                      {
                        "ids": { "tmdb": 578 },
                        "keywords": [ { "id": 96, "name": "monk" } ]
                      }
                    ]
                    """)
            };
        });

        var apiClient = CreateApiClient(handler);
        var provider = new MdbListKeywordTransportProvider(apiClient);
        var result = await provider.FetchKeywordsBatchAsync(CatalogContentType.Movie, [578]);

        Assert.NotNull(result);
        Assert.True(result!.KeywordsByTmdbId.ContainsKey(578));
        Assert.Equal("monk", result.KeywordsByTmdbId[578][0].Name);
        Assert.Equal(96, result.KeywordsByTmdbId[578][0].ExternalId);
    }

    [Fact]
    public async Task BatchKeywordFetchRejectsMoreThanTwoHundredIds()
    {
        var apiClient = CreateApiClient(new MockHttpMessageHandler());
        var ids = Enumerable.Range(1, 201).ToList();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            apiClient.PostTmdbMediaInfoBatchAsync("movie", ids, ["keyword"]));
    }

    [Fact]
    public async Task BatchKeywordFetchMapsEachItemBackToTmdbId()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                [
                  { "ids": { "tmdb": 10 }, "keywords": [] },
                  { "ids": { "tmdb": 20 }, "keywords": [ { "id": 1, "name": "a" } ] }
                ]
                """)
        });

        var provider = new MdbListKeywordTransportProvider(CreateApiClient(handler));
        var result = await provider.FetchKeywordsBatchAsync(CatalogContentType.Tv, [10, 20]);

        Assert.NotNull(result);
        Assert.True(result!.KeywordsByTmdbId.ContainsKey(10));
        Assert.Empty(result.KeywordsByTmdbId[10]);
        Assert.Single(result.KeywordsByTmdbId[20]);
    }

    [Fact]
    public async Task BatchKeywordFetchReturnsNullOnServerErrorWithoutThrowing()
    {
        var handler = new MockHttpMessageHandler();
        handler.EnqueueResponse(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var provider = new MdbListKeywordTransportProvider(CreateApiClient(handler));
        var result = await provider.FetchKeywordsBatchAsync(CatalogContentType.Movie, [1]);

        Assert.Null(result);
    }

    private static MdbListApiClient CreateApiClient(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.mdblist.com/", UriKind.Absolute)
        };

        return new MdbListApiClient(
            httpClient,
            Options.Create(new MdbListOptions { ApiKey = "test-key", BaseUrl = "https://api.mdblist.com/" }),
            NullLogger<MdbListApiClient>.Instance);
    }
}
