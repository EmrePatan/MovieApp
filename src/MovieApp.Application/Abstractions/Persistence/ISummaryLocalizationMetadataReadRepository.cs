using MovieApp.Application.Models.Localization;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ISummaryLocalizationMetadataReadRepository
{
    Task<SummaryLocalizationMetadataBatch> LoadAsync(
        IReadOnlyList<Guid> movieIds,
        IReadOnlyList<Guid> tvIds,
        IReadOnlyList<ContentLocalizedPosterKey> posterKeys,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
