using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Keywords;

/// <summary>
/// Global keyword classification governs whether a keyword participates in runtime semantics.
/// Per-locale translation quality is separate (localization review state).
/// </summary>
public static class KeywordSemanticEligibility
{
    public static bool IsRuntimeUsable(KeywordClassificationStatus status) =>
        status is KeywordClassificationStatus.Auto or KeywordClassificationStatus.Approved;

    public static bool IsExcluded(KeywordClassificationStatus status) =>
        status == KeywordClassificationStatus.Excluded;
}
