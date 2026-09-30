using MovieApp.Application.Models.Keywords;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Providers.MdbList;

internal static class MdbListKeywordTransportMapper
{
    internal static IReadOnlyList<MdbListKeywordTransportItem> MapKeywords(
        IEnumerable<MdbListKeywordJson>? rawKeywords)
    {
        if (rawKeywords is null)
        {
            return [];
        }

        var keywords = new List<MdbListKeywordTransportItem>();
        foreach (var raw in rawKeywords)
        {
            if (raw.Id <= 0 || string.IsNullOrWhiteSpace(raw.Name))
            {
                continue;
            }

            keywords.Add(new MdbListKeywordTransportItem(
                KeywordProvider.MdbList,
                raw.Id,
                raw.Name.Trim()));
        }

        return keywords;
    }
}
