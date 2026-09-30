using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class UserExternalLoginRepository(ApplicationDbContext dbContext) : IUserExternalLoginRepository
{
    public async Task<User?> GetUserByProviderAndSubjectAsync(
        string provider,
        string providerSubject,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.UserExternalLogins
            .AsNoTracking()
            .Where(login => login.Provider == provider && login.ProviderSubject == providerSubject)
            .Select(login => login.User)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UserExternalLogin> CreateAsync(
        UserExternalLogin externalLogin,
        CancellationToken cancellationToken = default)
    {
        try
        {
            dbContext.UserExternalLogins.Add(externalLogin);
            await dbContext.SaveChangesAsync(cancellationToken);
            return externalLogin;
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionExtensions.IsUniqueConstraintViolation(exception))
        {
            throw new ConflictException("This social account is already linked to another MovieApp user.");
        }
    }

    public async Task<IReadOnlyList<string>> GetProvidersForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.UserExternalLogins
            .AsNoTracking()
            .Where(login => login.UserId == userId)
            .Select(login => login.Provider)
            .Distinct()
            .OrderBy(provider => provider)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        Guid userId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        var login = await dbContext.UserExternalLogins
            .FirstOrDefaultAsync(
                entry => entry.UserId == userId && entry.Provider == provider,
                cancellationToken);

        if (login is null)
        {
            return false;
        }

        dbContext.UserExternalLogins.Remove(login);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task UpdateProviderPictureUrlAsync(
        Guid userId,
        string provider,
        string? pictureUrl,
        CancellationToken cancellationToken = default)
    {
        var login = await dbContext.UserExternalLogins
            .FirstOrDefaultAsync(
                entry => entry.UserId == userId && entry.Provider == provider,
                cancellationToken);

        if (login is null)
        {
            return;
        }

        var normalized = string.IsNullOrWhiteSpace(pictureUrl) ? null : pictureUrl.Trim();
        if (string.Equals(login.ProviderPictureUrl, normalized, StringComparison.Ordinal))
        {
            return;
        }

        login.SetProviderPictureUrl(normalized);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
