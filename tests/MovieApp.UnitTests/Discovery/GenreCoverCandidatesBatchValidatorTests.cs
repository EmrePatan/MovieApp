using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.Discovery;

public sealed class GenreCoverCandidatesBatchValidatorTests
{
    [Fact]
    public void CreateCriteria_DeduplicatesGenreIdsWhilePreservingOrder()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var criteria = GenreCoverCandidatesBatchValidator.CreateCriteria(
            [first.ToString(), second.ToString(), first.ToString()],
            "all",
            10);

        Assert.Equal([first, second], criteria.GenreIds);
        Assert.Equal(10, criteria.CandidateCount);
        Assert.Equal(SearchContentType.All, criteria.MediaType);
    }

    [Fact]
    public void ValidateRequest_RejectsEmptyGenreList()
    {
        var validation = GenreCoverCandidatesBatchValidator.ValidateRequest([], "all", 10);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void ValidateRequest_RejectsWhitespaceGenreId()
    {
        var validation = GenreCoverCandidatesBatchValidator.ValidateRequest(["   "], "all", 10);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void ValidateRequest_RejectsExcessiveBatchSize()
    {
        var ids = Enumerable.Range(0, DiscoveryBatchOrchestration.MaxGenreCoverBatchSize + 1)
            .Select(_ => Guid.NewGuid().ToString())
            .ToList();

        var validation = GenreCoverCandidatesBatchValidator.ValidateRequest(ids, "all", 10);

        Assert.False(validation.IsValid);
    }

    [Fact]
    public void CreateCriteria_ThrowsForInvalidCandidateCount()
    {
        Assert.Throws<ValidationException>(() =>
            GenreCoverCandidatesBatchValidator.CreateCriteria(
                [Guid.NewGuid().ToString()],
                "all",
                0));
    }
}
