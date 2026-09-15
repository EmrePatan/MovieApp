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
}
