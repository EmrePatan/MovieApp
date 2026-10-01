using MovieApp.Application.Configuration;
using MovieApp.Application.Services.Keywords;

namespace MovieApp.UnitTests.Keywords;

public sealed class KeywordDisplayQualityEvaluatorTests
{
    private static readonly KeywordDisplayProfileOptions DefaultOptions = new()
    {
        MinimumDocumentFrequency = 3,
        MaximumGenericDocumentFrequencyRatio = 0.12,
    };

    [Fact]
    public void Evaluate_SelectsThematicKeyword()
    {
        var result = KeywordDisplayQualityEvaluator.Evaluate(
            "zombie apocalypse",
            documentFrequency: 120,
            catalogDocumentCount: 5000,
            movieTitleCount: 80,
            tvTitleCount: 10,
            DefaultOptions);

        Assert.True(result.Displayable);
        Assert.True(result.DisplayRank > 0);
    }

    [Fact]
    public void Evaluate_ExcludesIncidentalNoiseKeyword()
    {
        var result = KeywordDisplayQualityEvaluator.Evaluate(
            "elevator",
            documentFrequency: 80,
            catalogDocumentCount: 5000,
            movieTitleCount: 60,
            tvTitleCount: 5,
            DefaultOptions);

        Assert.False(result.Displayable);
    }

    [Fact]
    public void Evaluate_ExcludesUltraRareKeyword()
    {
        var result = KeywordDisplayQualityEvaluator.Evaluate(
            "time travel",
            documentFrequency: 1,
            catalogDocumentCount: 5000,
            movieTitleCount: 1,
            tvTitleCount: 0,
            DefaultOptions);

        Assert.False(result.Displayable);
    }

    [Fact]
    public void Evaluate_ExcludesUltraGenericKeyword()
    {
        var result = KeywordDisplayQualityEvaluator.Evaluate(
            "drama",
            documentFrequency: 900,
            catalogDocumentCount: 1000,
            movieTitleCount: 500,
            tvTitleCount: 200,
            DefaultOptions);

        Assert.False(result.Displayable);
    }

    [Fact]
    public void Evaluate_ExcludesMiddleDfLexicalNoise()
    {
        var result = KeywordDisplayQualityEvaluator.Evaluate(
            "photograph",
            documentFrequency: 45,
            catalogDocumentCount: 5000,
            movieTitleCount: 30,
            tvTitleCount: 8,
            DefaultOptions);

        Assert.False(result.Displayable);
    }

    [Fact]
    public void Evaluate_DisplayRankOrdersThematicAbovePlainCoverage()
    {
        var thematic = KeywordDisplayQualityEvaluator.Evaluate(
            "survival",
            documentFrequency: 50,
            catalogDocumentCount: 5000,
            movieTitleCount: 40,
            tvTitleCount: 5,
            DefaultOptions);

        var plain = KeywordDisplayQualityEvaluator.Evaluate(
            "wilderness",
            documentFrequency: 50,
            catalogDocumentCount: 5000,
            movieTitleCount: 40,
            tvTitleCount: 5,
            DefaultOptions);

        Assert.True(thematic.Displayable);
        Assert.True(plain.Displayable);
        Assert.True(thematic.DisplayRank > plain.DisplayRank);
    }
}
