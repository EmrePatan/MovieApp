using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Localization;

public sealed class GenreLocalizationTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("en")]
    public void Localize_ReturnsCanonicalName_ForEnglish(string contentLocale)
    {
        Assert.Equal("Action", GenreLocalization.Localize("Action", contentLocale));
        Assert.Equal("Science Fiction", GenreLocalization.Localize("Science Fiction", contentLocale));
    }

    [Theory]
    [InlineData("tr-TR", "Aksiyon")]
    [InlineData("tr", "Komedi")]
    [InlineData("tr-TR", "Bilim Kurgu")]
    public void Localize_ReturnsTurkishName_ForTurkishLocale(string contentLocale, string expected)
    {
        var canonical = expected switch
        {
            "Aksiyon" => "Action",
            "Komedi" => "Comedy",
            "Bilim Kurgu" => "Science Fiction",
            _ => throw new InvalidOperationException()
        };

        Assert.Equal(expected, GenreLocalization.Localize(canonical, contentLocale));
    }

    [Theory]
    [InlineData("es-ES", "Acción")]
    [InlineData("es", "Comedia")]
    [InlineData("es-ES", "Ciencia ficción")]
    public void Localize_ReturnsSpanishName_ForSpanishLocale(string contentLocale, string expected)
    {
        var canonical = expected switch
        {
            "Acción" => "Action",
            "Comedia" => "Comedy",
            "Ciencia ficción" => "Science Fiction",
            _ => throw new InvalidOperationException()
        };

        Assert.Equal(expected, GenreLocalization.Localize(canonical, contentLocale));
    }

    [Theory]
    [InlineData("de-DE", "Komödie")]
    [InlineData("fr-FR", "Comédie")]
    [InlineData("it-IT", "Commedia")]
    [InlineData("pt-BR", "Comédia")]
    public void Localize_ReturnsLocalizedName_ForNewLocales(string contentLocale, string expected)
    {
        Assert.Equal(expected, GenreLocalization.Localize("Comedy", contentLocale));
    }

    [Fact]
    public void Localize_FallsBackToCanonical_ForUnknownGenre()
    {
        Assert.Equal(
            "Experimental",
            GenreLocalization.Localize("Experimental", ContentLocaleResolver.SpanishSpain));
    }
}
