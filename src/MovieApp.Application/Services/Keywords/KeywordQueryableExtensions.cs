using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Keywords;

public static class KeywordQueryableExtensions
{
    public static IQueryable<Keyword> WhereRuntimeUsable(this IQueryable<Keyword> query) =>
        query.Where(keyword => keyword.ClassificationStatus != KeywordClassificationStatus.Excluded);
}
