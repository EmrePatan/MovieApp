using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class MoviePerson
{
    public Guid MovieId { get; set; }

    public Movie Movie { get; set; } = null!;

    public Guid PersonId { get; set; }

    public Person Person { get; set; } = null!;

    public string? Character { get; set; }

    public string? Job { get; set; }

    public string? Department { get; set; }

    public CreditType CreditType { get; set; }
}
