using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Identity;
using MovieApp.Domain.Users;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class UserAvatarReadRepository(ApplicationDbContext dbContext) : IUserAvatarReadRepository
{
    public async Task<UserAvatarSources?> GetSourcesForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var map = await GetSourcesForUsersAsync([userId], cancellationToken);
        return map.TryGetValue(userId, out var sources) ? sources : null;
    }

    public async Task<IReadOnlyDictionary<Guid, UserAvatarSources>> GetSourcesForUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, UserAvatarSources>();
        }

        var distinctIds = userIds.Distinct().ToArray();

        var userRows = await dbContext.Users
            .AsNoTracking()
            .Where(user => distinctIds.Contains(user.Id))
            .Select(user => new { user.Id, user.CustomAvatarStorageKey })
            .ToListAsync(cancellationToken);

        var googlePictures = await dbContext.UserExternalLogins
            .AsNoTracking()
            .Where(login =>
                distinctIds.Contains(login.UserId) &&
                login.Provider == ExternalLoginProviders.Google)
            .Select(login => new { login.UserId, login.ProviderPictureUrl })
            .ToListAsync(cancellationToken);

        var googleByUser = googlePictures.ToDictionary(row => row.UserId, row => row.ProviderPictureUrl);

        var result = new Dictionary<Guid, UserAvatarSources>(userRows.Count);
        foreach (var row in userRows)
        {
            googleByUser.TryGetValue(row.Id, out var googlePicture);
            result[row.Id] = new UserAvatarSources(row.Id, row.CustomAvatarStorageKey, googlePicture);
        }

        return result;
    }
}
