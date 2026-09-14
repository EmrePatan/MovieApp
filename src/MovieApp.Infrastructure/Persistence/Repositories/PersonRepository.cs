using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class PersonRepository(ApplicationDbContext dbContext) : IPersonRepository
{
    public async Task<Person?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
        await dbContext.People
            .AsNoTracking()
            .FirstOrDefaultAsync(person => person.TmdbId == tmdbId, cancellationToken);

    public async Task<Person> UpsertFromProviderAsync(
        int tmdbId,
        string name,
        string? profilePath,
        CancellationToken cancellationToken = default)
    {
        var person = await dbContext.People
            .FirstOrDefaultAsync(existingPerson => existingPerson.TmdbId == tmdbId, cancellationToken);

        var utcNow = DateTime.UtcNow;

        if (person is null)
        {
            person = new Person
            {
                Id = Guid.NewGuid(),
                TmdbId = tmdbId,
                Name = name,
                ProfilePath = profilePath,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

            dbContext.People.Add(person);
        }
        else
        {
            person.Name = name;
            person.ProfilePath = profilePath;
            person.UpdatedAt = utcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return person;
    }
}
