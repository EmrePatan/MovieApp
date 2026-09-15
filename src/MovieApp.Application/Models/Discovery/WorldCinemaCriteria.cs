using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Models.Discovery;

public sealed record WorldCinemaCriteria(
    SearchContentType MediaType,
    string OriginCountry,
    AdvancedDiscoverSort Sort,
    int Page,
    int PageSize);
