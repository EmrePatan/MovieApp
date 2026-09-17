using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Infrastructure.AiRecommendations;

namespace MovieApp.UnitTests.AiRecommendations;

public sealed class GeminiAiMovieRecommendationProviderTests
{
    [Fact]
    public void ParseResponseReturnsValidStructuredSuggestions()
    {
        var responseBody = """
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      {
                        "text": "{\"suggestions\":[{\"title\":\"Arrival\",\"year\":2016,\"mediaType\":\"movie\",\"tmdbId\":329996,\"reason\":\"Mind-bending sci-fi\"}]}"
                      }
                    ]
                  }
                }
              ]
            }
            """;

        var result = GeminiAiMovieRecommendationProvider.ParseResponse(responseBody);

        Assert.Single(result.Suggestions);
        Assert.Equal("Arrival", result.Suggestions[0].Title);
        Assert.Equal("movie", result.Suggestions[0].MediaType);
    }

    [Fact]
    public void ParseResponseThrowsForMalformedJson()
    {
        var responseBody = """
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      { "text": "not-json" }
                    ]
                  }
                }
              ]
            }
            """;

        Assert.Throws<AiRecommendationProviderException>(() =>
            GeminiAiMovieRecommendationProvider.ParseResponse(responseBody));
    }

    [Fact]
    public void ParseResponseAcceptsSuggestionsEvenWhenMediaTypeIsWrongForDownstreamValidation()
    {
        var responseBody = """
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      {
                        "text": "{\"suggestions\":[{\"title\":\"Breaking Bad\",\"year\":2008,\"mediaType\":\"tv\",\"reason\":\"Great show\"}]}"
                      }
                    ]
                  }
                }
              ]
            }
            """;

        var result = GeminiAiMovieRecommendationProvider.ParseResponse(responseBody);

        Assert.Equal("tv", result.Suggestions[0].MediaType);
    }

    [Fact]
    public void BuildRequestBodyIncludesUserMessageAndSuggestionCount()
    {
        var request = new AiProviderRequest(
            "Something mysterious with a twist",
            new AiTasteProfile([], [], [], [], [], [], [], [], true),
            new AiRecommendationSessionState { SessionId = Guid.NewGuid() },
            8);

        var body = GeminiAiMovieRecommendationProvider.BuildRequestBody(request);

        Assert.Contains("Something mysterious with a twist", body, StringComparison.Ordinal);
        Assert.Contains("responseMimeType", body, StringComparison.Ordinal);
        Assert.Contains("suggestions", body, StringComparison.Ordinal);
    }
}
