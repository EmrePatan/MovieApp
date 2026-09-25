using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Identity;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(
    ApplicationDbContext dbContext,
    IMemoryCache? memoryCache = null) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public async Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public async Task<Guid?> GetSecurityStampAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == id && user.IsActive)
            .Select(user => (Guid?)user.SecurityStamp)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .FirstOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    public async Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        try
        {
            dbContext.Users.Update(user);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (memoryCache is not null)
            {
                SecurityStampCache.Invalidate(memoryCache, user.Id);
            }
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionExtensions.IsUniqueConstraintViolation(exception))
        {
            throw new ConflictException("A user with this email address already exists.");
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(existingUser => existingUser.Id == id, cancellationToken);

        if (user is null)
        {
            return false;
        }

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (memoryCache is not null)
        {
            SecurityStampCache.Invalidate(memoryCache, id);
        }

        return true;
    }
}
