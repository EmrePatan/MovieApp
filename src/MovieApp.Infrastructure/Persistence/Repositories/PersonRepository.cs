using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Providers;
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

    public async Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
        IReadOnlyList<PersonProviderSummary> summaries,
        CancellationToken cancellationToken = default)
    {
        var tmdbIds = summaries
            .Select(summary => summary.TmdbId)
            .Distinct()
            .ToList();

        if (tmdbIds.Count == 0)
        {
            return new Dictionary<int, Guid>();
        }

        var existingPeople = await dbContext.People
            .AsNoTracking()
            .Where(person => person.TmdbId.HasValue && tmdbIds.Contains(person.TmdbId.Value))
            .Select(person => new { person.TmdbId, person.Id })
            .ToListAsync(cancellationToken);

        var existingIds = existingPeople
            .Where(person => person.TmdbId.HasValue)
            .ToDictionary(person => person.TmdbId!.Value, person => person.Id);

        var mutableExistingIds = existingIds.ToDictionary(pair => pair.Key, pair => pair.Value);
        var utcNow = DateTime.UtcNow;
        var hasChanges = false;

        foreach (var summary in summaries)
        {
            if (mutableExistingIds.ContainsKey(summary.TmdbId))
            {
                continue;
            }

            var person = new Person
            {
                Id = Guid.NewGuid(),
                TmdbId = summary.TmdbId,
                Name = summary.Name,
                ProfilePath = summary.ProfilePath,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

            dbContext.People.Add(person);
            mutableExistingIds[summary.TmdbId] = person.Id;
            hasChanges = true;
        }

        if (!hasChanges)
        {
            return mutableExistingIds;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return mutableExistingIds;
    }
}
