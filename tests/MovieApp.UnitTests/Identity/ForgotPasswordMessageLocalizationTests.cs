using MovieApp.Application.Services.Identity;
using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Identity;

public sealed class ForgotPasswordMessageLocalizationTests
{
    [Fact]
    public void GetSuccessMessage_ReturnsEnglish_ForEnglishLocale()
    {
        Assert.Equal(
            ForgotPasswordMessageLocalization.EnglishSuccessMessage,
            ForgotPasswordMessageLocalization.GetSuccessMessage(ContentLocaleResolver.EnglishUnitedStates));
    }

    [Fact]
    public void GetSuccessMessage_ReturnsTurkish_ForTurkishLocale()
    {
        Assert.Equal(
            ForgotPasswordMessageLocalization.TurkishSuccessMessage,
            ForgotPasswordMessageLocalization.GetSuccessMessage(ContentLocaleResolver.TurkishTurkey));
    }

    [Fact]
    public void GetSuccessMessage_ReturnsSpanish_ForSpanishLocale()
    {
        Assert.Equal(
            ForgotPasswordMessageLocalization.SpanishSuccessMessage,
            ForgotPasswordMessageLocalization.GetSuccessMessage(ContentLocaleResolver.SpanishSpain));
    }
}
