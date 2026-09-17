using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.AiRecommendations;
using Microsoft.Extensions.Options;

namespace MovieApp.Application.Services.AiRecommendations;

public sealed class AiTasteProfileBuilder(
    IAiTasteProfileDataSource dataSource,
    ICacheService cacheService,
    IOptions<AiRecommendationOptions> options) : IAiTasteProfileBuilder
{
    private const int MaxTopGenres = 8;
    private const int MaxAvoidedGenres = 8;
    private const int MaxHighRatings = 10;
    private const int MaxLowRatings = 5;
    private const int MaxFavorites = 10;
    private const int MaxWatchlistHints = 5;
    private const int MaxWeakTvGenres = 5;

    public async Task<AiTasteProfile> BuildAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = AiRecommendationCacheKeys.TasteProfile(userId);
        var cached = await cacheService.GetAsync<AiTasteProfile>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var raw = await dataSource.LoadAsync(userId, cancellationToken);
        var profile = BuildFromRaw(raw);

        var ttl = TimeSpan.FromMinutes(Math.Max(1, options.Value.TasteProfileCacheMinutes));
        await cacheService.SetAsync(cacheKey, profile, ttl, cancellationToken);

        return profile;
    }

    internal static AiTasteProfile BuildFromRaw(AiTasteProfileRawData raw)
    {
        var genreScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        var highRatings = raw.Ratings
            .Where(rating => rating.MovieId.HasValue && rating.Score >= 8)
            .OrderByDescending(rating => rating.Score)
            .Take(MaxHighRatings)
            .Select(rating => new AiTasteMovieSignal(
                rating.MovieTitle ?? "Unknown",
                rating.MovieYear,
                rating.Score))
            .ToList();

        var lowRatings = raw.Ratings
            .Where(rating => rating.MovieId.HasValue && rating.Score <= 5)
            .OrderBy(rating => rating.Score)
            .Take(MaxLowRatings)
            .Select(rating => new AiTasteMovieSignal(
                rating.MovieTitle ?? "Unknown",
                rating.MovieYear,
                rating.Score))
            .ToList();

        foreach (var rating in raw.Ratings.Where(r => r.MovieId.HasValue))
        {
            if (rating.Score >= 8)
            {
                AddGenreWeights(genreScores, rating.MovieGenres, 1.0);
            }
            else if (rating.Score <= 5)
            {
                AddGenreWeights(genreScores, rating.MovieGenres, -0.8);
            }
        }

        var favorites = raw.Favorites
            .Take(MaxFavorites)
            .Select(movie => new AiTasteMovieSignal(movie.Title, movie.Year, null))
            .ToList();

        foreach (var favorite in raw.Favorites)
        {
            AddGenreWeights(genreScores, favorite.Genres, 1.2);
        }

        var watchlistHints = raw.WatchlistMovies
            .Take(MaxWatchlistHints)
            .Select(movie => new AiTasteMovieSignal(movie.Title, movie.Year, null))
            .ToList();

        foreach (var watchlistMovie in raw.WatchlistMovies)
        {
            AddGenreWeights(genreScores, watchlistMovie.Genres, 0.3);
        }

        var weakTvGenres = raw.WatchedTvGenres
            .OrderByDescending(row => row.WatchCount)
            .Take(MaxWeakTvGenres)
            .Select(row => new AiTasteGenreAffinity(row.Genre, Math.Min(0.4, row.WatchCount * 0.05)))
            .ToList();

        foreach (var tvGenre in weakTvGenres)
        {
            if (!genreScores.ContainsKey(tvGenre.Genre))
            {
                genreScores[tvGenre.Genre] = tvGenre.Weight;
            }
        }

        var topGenres = genreScores
            .Where(pair => pair.Value > 0)
            .OrderByDescending(pair => pair.Value)
            .Take(MaxTopGenres)
            .Select(pair => new AiTasteGenreAffinity(pair.Key, pair.Value))
            .ToList();

        var avoidedGenres = genreScores
            .Where(pair => pair.Value < 0)
            .OrderBy(pair => pair.Value)
            .Take(MaxAvoidedGenres)
            .Select(pair => new AiTasteGenreAffinity(pair.Key, Math.Abs(pair.Value)))
            .ToList();

        var isColdStart = highRatings.Count == 0 &&
                          lowRatings.Count == 0 &&
                          favorites.Count == 0 &&
                          watchlistHints.Count == 0 &&
                          topGenres.Count == 0;

        return new AiTasteProfile(
            topGenres,
            avoidedGenres,
            highRatings,
            lowRatings,
            favorites,
            watchlistHints,
            weakTvGenres,
            [],
            isColdStart);
    }

    private static void AddGenreWeights(
        Dictionary<string, double> genreScores,
        IReadOnlyList<string> genres,
        double weight)
    {
        foreach (var genre in genres)
        {
            if (string.IsNullOrWhiteSpace(genre))
            {
                continue;
            }

            if (genreScores.TryGetValue(genre, out var current))
            {
                genreScores[genre] = current + weight;
            }
            else
            {
                genreScores[genre] = weight;
            }
        }
    }
}
