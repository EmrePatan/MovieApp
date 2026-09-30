using MovieApp.Domain.Enums;

namespace MovieApp.Application.Models.Keywords;

public sealed record MdbListKeywordTransportItem(
    KeywordProvider Provider,
    int ExternalId,
    string Name);
