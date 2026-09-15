using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Notifications;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.Notifications;

public sealed class GetNotificationsService(
    ICurrentUser currentUser,
    IUserReleaseNotificationRepository notificationRepository) : IGetNotificationsService
{
    public async Task<NotificationsListResult> GetAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var validationResult = SearchPaginationValidator.Validate(page, pageSize);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage!);
        }

        var (items, totalCount) = await notificationRepository.GetInboxAsync(
            userId,
            page,
            pageSize,
            cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new NotificationsListResult(items, page, pageSize, totalCount, totalPages);
    }
}
