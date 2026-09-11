using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Movie> Movies => Set<Movie>();

    public DbSet<TvShow> TvShows => Set<TvShow>();

    public DbSet<Genre> Genres => Set<Genre>();

    public DbSet<MovieGenre> MovieGenres => Set<MovieGenre>();

    public DbSet<TvShowGenre> TvShowGenres => Set<TvShowGenre>();

    public DbSet<Person> People => Set<Person>();

    public DbSet<MoviePerson> MoviePeople => Set<MoviePerson>();

    public DbSet<TvShowPerson> TvShowPeople => Set<TvShowPerson>();

    public DbSet<Season> Seasons => Set<Season>();

    public DbSet<Episode> Episodes => Set<Episode>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Favorite> Favorites => Set<Favorite>();

    public DbSet<Watchlist> Watchlists => Set<Watchlist>();

    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

    public DbSet<Rating> Ratings => Set<Rating>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<WatchedMovie> WatchedMovies => Set<WatchedMovie>();

    public DbSet<WatchedEpisode> WatchedEpisodes => Set<WatchedEpisode>();

    public DbSet<SearchHistory> SearchHistories => Set<SearchHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
