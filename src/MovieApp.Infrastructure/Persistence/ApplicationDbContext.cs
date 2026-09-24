using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext, IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Movie> Movies => Set<Movie>();

    public DbSet<MovieRegionalRelease> MovieRegionalReleases => Set<MovieRegionalRelease>();

    public DbSet<TvShow> TvShows => Set<TvShow>();

    public DbSet<Genre> Genres => Set<Genre>();

    public DbSet<MovieGenre> MovieGenres => Set<MovieGenre>();

    public DbSet<TvShowGenre> TvShowGenres => Set<TvShowGenre>();

    public DbSet<Keyword> Keywords => Set<Keyword>();

    public DbSet<MovieKeyword> MovieKeywords => Set<MovieKeyword>();

    public DbSet<TvShowKeyword> TvShowKeywords => Set<TvShowKeyword>();

    public DbSet<Person> People => Set<Person>();

    public DbSet<MoviePerson> MoviePeople => Set<MoviePerson>();

    public DbSet<TvShowPerson> TvShowPeople => Set<TvShowPerson>();

    public DbSet<Season> Seasons => Set<Season>();

    public DbSet<Episode> Episodes => Set<Episode>();

    public DbSet<User> Users => Set<User>();

    public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();

    public DbSet<Favorite> Favorites => Set<Favorite>();

    public DbSet<Watchlist> Watchlists => Set<Watchlist>();

    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

    public DbSet<Rating> Ratings => Set<Rating>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<WatchedMovie> WatchedMovies => Set<WatchedMovie>();

    public DbSet<WatchedEpisode> WatchedEpisodes => Set<WatchedEpisode>();

    public DbSet<SearchHistory> SearchHistories => Set<SearchHistory>();

    public DbSet<SearchProviderRefresh> SearchProviderRefreshes => Set<SearchProviderRefresh>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();

    public DbSet<CatalogFollow> CatalogFollows => Set<CatalogFollow>();

    public DbSet<CatalogReleaseEvent> CatalogReleaseEvents => Set<CatalogReleaseEvent>();

    public DbSet<UserReleaseNotification> UserReleaseNotifications => Set<UserReleaseNotification>();

    public DbSet<UserReleaseNotificationEvent> UserReleaseNotificationEvents => Set<UserReleaseNotificationEvent>();

    public DbSet<TvShowCatalogSyncState> TvShowCatalogSyncStates => Set<TvShowCatalogSyncState>();

    public DbSet<TmdbTvChangesSyncCheckpoint> TmdbTvChangesSyncCheckpoints => Set<TmdbTvChangesSyncCheckpoint>();

    public DbSet<PushDevice> PushDevices => Set<PushDevice>();

    public DbSet<PushNotificationDelivery> PushNotificationDeliveries => Set<PushNotificationDelivery>();

    public DbSet<ProductMetricDaily> ProductMetricDaily => Set<ProductMetricDaily>();

    public DbSet<ExternalRatingSnapshot> ExternalRatingSnapshots => Set<ExternalRatingSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        await Database.ExecuteInRetriableTransactionAsync(
            async ct =>
            {
                await action(ct);
                await SaveChangesAsync(ct);
            },
            cancellationToken);
    }
}
