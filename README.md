# MovieApp

Commercial movie and TV tracking application backend built with .NET 10 and Clean Architecture.

## Architecture

MovieApp follows Clean Architecture with strict dependency direction:

```text
Api
  ↓
Application
  ↓
Domain

Infrastructure
  ↓
Application
  ↓
Domain
```

- **Domain** contains core business primitives and has no external dependencies.
- **Application** defines use cases and infrastructure abstractions (interfaces).
- **Infrastructure** implements persistence, caching, and external provider configuration.
- **Contracts** contains API request/response DTOs exposed to clients.
- **Api** hosts HTTP endpoints, middleware, and application composition.

## Solution Structure

```text
MovieApp/
├── src/
│   ├── MovieApp.Api/              # Web API, middleware, composition root
│   ├── MovieApp.Application/      # Application abstractions and services
│   ├── MovieApp.Domain/           # Domain primitives
│   ├── MovieApp.Infrastructure/   # EF Core, Redis, provider configuration
│   └── MovieApp.Contracts/        # Public API contracts (DTOs)
├── tests/
│   ├── MovieApp.UnitTests/        # Unit tests
│   └── MovieApp.IntegrationTests/ # API integration tests
├── docker-compose.yml
├── .env.example
├── MovieApp.sln
├── global.json
├── Directory.Build.props
└── Directory.Packages.props
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) or Docker Engine with Docker Compose v2

For local development, PostgreSQL and Redis are provided via Docker Compose. You do not need to install them separately on your machine.

## Configuration

Configuration is loaded from `appsettings.json` and environment-specific files.

Never commit secrets. Provide credentials using environment variables, user secrets, or a secure secret store.

### PostgreSQL

Development settings in `appsettings.Development.json`:

```json
"PostgreSql": {
  "Host": "localhost",
  "Port": 5432,
  "Database": "movieapp",
  "Username": "movieapp"
}
```

Provide the password using environment configuration (never commit it):

```bash
# PowerShell
$env:PostgreSql__Password = "your-local-development-password"

# bash
export PostgreSql__Password="your-local-development-password"
```

You can also supply a full connection string via `PostgreSql__ConnectionString`.

### Redis

```json
"Redis": {
  "ConnectionString": "localhost:6379",
  "InstanceName": "MovieApp:"
}
```

### External Movie Providers

MovieApp is a commercial product. Do not assume TMDB's free API tier is permitted for production use. Configure a properly licensed TMDB account before enabling the TMDB provider in production.

```json
"MovieProviders": {
  "Provider": "Fake",
  "Tmdb": {
    "BaseUrl": "https://api.themoviedb.org/3/",
    "ApiKey": "",
    "ReadAccessToken": ""
  },
  "Tvdb": {
    "BaseUrl": "https://api4.thetvdb.com/v4/",
    "ApiKey": ""
  }
}
```

- `Provider`: `Fake` (default, local development and tests) or `Tmdb` — controls **both** movie and TV catalog providers
- `ReadAccessToken`: preferred TMDB authentication via bearer token
- `ApiKey`: supported fallback authentication via query string when no bearer token is configured

Secrets must come from environment variables or .NET User Secrets. Never commit credentials to source control.

### Local user secrets (alternative for development)

```bash
cd src/MovieApp.Api
dotnet user-secrets init
dotnet user-secrets set "PostgreSql:Password" "YOUR_PASSWORD"

# Keep Fake provider for local development without TMDB credentials
dotnet user-secrets set "MovieProviders:Provider" "Fake"

# To use TMDB locally with a licensed account:
dotnet user-secrets set "MovieProviders:Provider" "Tmdb"
dotnet user-secrets set "MovieProviders:Tmdb:ReadAccessToken" "YOUR_READ_ACCESS_TOKEN"
# or
dotnet user-secrets set "MovieProviders:Tmdb:ApiKey" "YOUR_API_KEY"
```

Environment variable equivalents:

```bash
MovieProviders__Provider=Tmdb
MovieProviders__Tmdb__ReadAccessToken=YOUR_READ_ACCESS_TOKEN
```

## Local Development Infrastructure (Docker Compose)

MovieApp uses Docker Compose to run PostgreSQL and Redis for local development.

### Setup

1. Copy the environment template:

```bash
cp .env.example .env
```

2. Edit `.env` and set `POSTGRES_PASSWORD` and `PostgreSql__Password` to the same local development value.

`.env` is git-ignored and must not be committed.

### Start PostgreSQL and Redis

```bash
docker compose up -d
```

### Inspect container status

```bash
docker compose ps
```

Both services should report `healthy` once startup completes.

### View logs

```bash
docker compose logs
docker compose logs postgres
docker compose logs redis
```

### Stop containers

```bash
docker compose down
```

### Remove containers and volumes

```bash
docker compose down -v
```

This removes containers and deletes the named volumes `movieapp-postgres-data` and `movieapp-redis-data`, which clears persisted database and cache data.

### Persistent volumes

- `movieapp-postgres-data` stores PostgreSQL data under `/var/lib/postgresql/data`
- `movieapp-redis-data` stores Redis AOF data under `/data`

Data survives `docker compose down` and is removed only with `docker compose down -v`.

### Services

| Service    | Image           | Host port |
|------------|-----------------|-----------|
| PostgreSQL | `postgres:17`   | `5432`    |
| Redis      | `redis:7-alpine`| `6379`    |

## Run the API

```bash
dotnet restore
dotnet build
dotnet run --project src/MovieApp.Api
```

Endpoints:

- `GET /health` - application health response
- `GET /health/ready` - infrastructure readiness checks (PostgreSQL, Redis when configured)
- `GET /swagger` - Swagger UI (Development environment)

## Movie Catalog API

### Search movies

`GET /api/movies/search?q={query}&page={page}&pageSize={pageSize}`

| Parameter | Required | Default | Rules |
|-----------|----------|---------|-------|
| `q` | Yes | — | 2–100 characters after trim |
| `page` | No | `1` | Minimum `1` |
| `pageSize` | No | `20` | Minimum `1`, maximum `100` |

Example:

```http
GET /api/movies/search?q=interstellar
GET /api/movies/search?q=interstellar&page=1&pageSize=20
```

Response:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "totalPages": 0,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

Invalid query or pagination values return HTTP `400`.

### Get movie by id

`GET /api/movies/{id}`

Returns persisted movie details by internal `Guid`. Returns HTTP `404` when the movie is not found.

### Provider behavior

- **Fake** (default): deterministic local data for development and tests. Pagination honors the requested `page` and `pageSize`.
- **TMDB**: uses TMDB movie search pagination. TMDB returns a fixed page size of 20 results per page and does not support arbitrary `pageSize` values. When TMDB is enabled, the API response `pageSize` reflects TMDB's actual page size (`20`), not the client-requested value.

Configure the active provider via `MovieProviders:Provider` (`Fake` or `Tmdb`). The same setting controls **both** movie and TV catalog providers. See [External Movie Providers](#external-movie-providers) for credential configuration.

## TV Catalog API

TV show catalog infrastructure uses the same Clean Architecture patterns as movies. **TVDB integration is not implemented yet.**

Provider selection follows `MovieProviders:Provider`:

- **`Fake`** (default): deterministic local data for development and tests
- **`Tmdb`**: real TMDB TV search, show, season, and episode hydration (requires configured TMDB credentials)

When TMDB is enabled, TV search uses TMDB pagination (fixed page size of 20). The API response `pageSize` reflects TMDB's actual page size, not necessarily the client-requested value.

### Search TV shows

`GET /api/tvshows/search?q={query}&page={page}&pageSize={pageSize}`

Uses the same query and pagination rules as movie search. Returns the same pagination envelope (`items`, `page`, `pageSize`, `totalCount`, `totalPages`, `hasNextPage`, `hasPreviousPage`).

Example:

```http
GET /api/tvshows/search?q=breaking
GET /api/tvshows/search?q=breaking&page=1&pageSize=20
```

### Get TV show by id

`GET /api/tvshows/{id}`

Returns persisted TV show details including genres and season summaries. Returns HTTP `404` when not found.

### Get season

`GET /api/tvshows/{id}/seasons/{seasonNumber}`

Returns season metadata and episode summaries. Hydrates from the configured TV provider when needed. Returns HTTP `404` when the TV show or season does not exist.

### Get episode

`GET /api/tvshows/{id}/seasons/{seasonNumber}/episodes/{episodeNumber}`

Returns episode details. Hydrates from the configured TV provider when needed. Returns HTTP `404` when the TV show, season, or episode does not exist.

### Fake TV provider behavior

When `MovieProviders:Provider` is `Fake` (default), TV search returns deterministic **Breaking Bad** data for queries containing `breaking`, including:

- Stable external ID: `fake-tv-900101`
- 3 seasons with multiple episodes
- Normalized image paths (no hardcoded CDN URLs)

### TMDB TV provider behavior

When `MovieProviders:Provider` is `Tmdb`, TV catalog data is fetched from TMDB and persisted to PostgreSQL using the same lazy upsert pattern as movies:

- Provider external IDs use the format `tmdb-{tmdbId}` (same as movies)
- Public API `{id}` values remain internal MovieApp GUIDs assigned on first upsert
- Search results are cached for 15 minutes (`tvshow-search:*` keys)
- Season and episode details are hydrated on demand when not already persisted
- TMDB season 0 ("Specials") is excluded from show season summaries
- TMDB 404 responses map to HTTP `404` through the existing application services
- TMDB rate limits are handled by the shared `TmdbApiClient` retry policy

Configure TMDB credentials under `MovieProviders:Tmdb` (see [External Movie Providers](#external-movie-providers)). Do not commit API keys or access tokens.

### Existing development data

Switching from `Fake` to `Tmdb` does **not** delete existing TV catalog rows or user data. Previously persisted synthetic TV shows remain addressable by their internal GUIDs. Lazy provider hydration for legacy fake `TmdbId` values will not resolve against TMDB; re-search TV titles to ingest real catalog data in production environments.

## Authentication

MovieApp uses a custom identity foundation (not ASP.NET Identity) with JWT Bearer tokens.

### Architecture

- **Domain:** `User` entity with normalized email and password hash storage
- **Application:** `RegisterUserService`, `LoginUserService`, `GetCurrentUserService`
- **Abstractions:** `IUserRepository`, `IPasswordHasher`, `ITokenService`, `ICurrentUser`
- **Infrastructure:** PBKDF2 password hashing, JWT token generation, EF Core `users` table
- **API:** `AuthController`, `HttpContextCurrentUser`, JWT middleware configuration

Movie and TV catalog endpoints remain publicly accessible. Favorites and watchlists require JWT authentication via `ICurrentUser`.

### Endpoints

| Endpoint | Auth | Success | Errors |
|----------|------|---------|--------|
| `POST /api/auth/register` | No | `201 Created` | `400` invalid input, `409` duplicate email |
| `POST /api/auth/login` | No | `200 OK` | `400` invalid input, `401` invalid credentials |
| `GET /api/auth/me` | Bearer JWT | `200 OK` | `401` missing/invalid token |

### JWT configuration

```json
"Authentication": {
  "Jwt": {
    "Issuer": "MovieApp",
    "Audience": "MovieApp.Mobile",
    "SigningKey": "",
    "AccessTokenMinutes": 60
  }
}
```

`SigningKey` must be provided via user secrets or environment variables. Never commit signing keys to source control.

### Local secret configuration

```bash
cd src/MovieApp.Api
dotnet user-secrets set "Authentication:Jwt:SigningKey" "YOUR_LOCAL_DEVELOPMENT_SIGNING_KEY_AT_LEAST_32_CHARS"
```

Or via environment variable:

```bash
Authentication__Jwt__SigningKey=YOUR_LOCAL_DEVELOPMENT_SIGNING_KEY_AT_LEAST_32_CHARS
```

### Example usage

```bash
# Register
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"StrongPassword123","displayName":"User"}'

# Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"StrongPassword123"}'

# Current user
curl http://localhost:5000/api/auth/me \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

### Security notes

- Passwords are hashed with PBKDF2 (SHA-256, unique salt per password, 100,000 iterations)
- Login failures return a generic `Invalid email or password.` message
- Inactive users cannot log in
- JWT validation enforces issuer, audience, signing key, and expiration
- Refresh tokens, email verification, and password reset are not implemented yet
- Rate limiting and brute-force protection are future infrastructure work

## User Profile & Account Management

Authenticated users can manage their profile, credentials, and account through dedicated endpoints. Identity always comes from the validated JWT (`ICurrentUser`); client requests never supply a `UserId`.

### Architecture

- **Domain:** `User` aggregate with `SecurityStamp`, display name/email/password change methods
- **Application:** `UserProfileService` with centralized `PasswordPolicyValidator` and `ProfileValidator`
- **Abstractions:** extended `IUserRepository`, `IUserStatisticsRepository`, `IUserProfileService`
- **Infrastructure:** SQL `COUNT` statistics queries, cascade delete for user-owned data
- **API:** `UsersController` (all endpoints require `[Authorize]`)

### Endpoints

| Endpoint | Success | Errors |
|----------|---------|--------|
| `GET /api/users/me` | `200 OK` | `401`, `404` |
| `PUT /api/users/me/profile` | `200 OK` | `400`, `401`, `404` |
| `PUT /api/users/me/email` | `200 OK` + new JWT | `400`, `401`, `404`, `409` |
| `PUT /api/users/me/password` | `200 OK` + new JWT | `400`, `401`, `404` |
| `GET /api/users/me/statistics` | `200 OK` | `401` |
| `DELETE /api/users/me` | `204 No Content` | `400`, `401`, `404` |

### Profile behavior

- `GET /api/users/me` returns `id`, `email`, `userName`, `displayName`, and `createdAt`
- `UserName` remains immutable (derived from the original registration email)
- `PUT /api/users/me/profile` updates only `displayName` (trimmed, 1–100 characters)

### Email and password changes

Sensitive operations require the current password in addition to a valid JWT:

- Email is normalized with the existing `UserEmailNormalizer`
- Duplicate emails return `409 Conflict` with a generic message
- Successful email/password changes return a new JWT in `UserProfileAuthResponse`

### JWT / session invalidation

Each user has a `SecurityStamp` included as a `security_stamp` JWT claim. After email or password change, the stamp rotates and a new token is issued. Existing tokens fail validation on the next request because the stamp no longer matches the database value. This provides session invalidation without refresh tokens or server-side session storage.

### Account deletion

`DELETE /api/users/me` permanently removes the user and cascades deletion of private data:

- favorites, watchlists, watchlist items, ratings, reviews, watch history, search history

Catalog data (movies, TV shows, seasons, episodes, genres, people) is never deleted.

### Statistics

`GET /api/users/me/statistics` returns database-level counts for favorites, watchlists, ratings, reviews, and watch history, plus aggregate totals (`totalRatingCount`, `totalReviewCount`, `totalWatchedCount`).

## Recommendation Foundation

This is **not an ML/AI recommendation engine yet**. Step 14 provides a deterministic, PostgreSQL-based recommendation foundation that can later be replaced by a more sophisticated engine without changing API contracts.

### Architecture

```text
API → IRecommendationService → Recommendation Engines → IRecommendationRepository → PostgreSQL
```

- **Application:** `RecommendationService`, `SimilarityEngine`, `PersonalizedRecommendationEngine`, `RecommendationOptions`
- **Infrastructure:** `RecommendationRepository` with bounded SQL candidate generation
- **Caching:** `ICacheService` via `RecommendationCacheKeys`
- **Algorithm version:** `v1` (included in personalized cache keys)

### Endpoints

| Endpoint | Auth | Description |
|----------|------|-------------|
| `GET /api/recommendations/movies/{movieId}/similar` | No | Similar movies |
| `GET /api/recommendations/tvshows/{tvShowId}/similar` | No | Similar TV shows |
| `GET /api/recommendations` | Bearer JWT | Personalized recommendations |
| `GET /api/recommendations/home` | Bearer JWT | Home recommendation sections |

### Recommendation signals

| Signal | Weight | Meaning |
|--------|--------|---------|
| Rating | `(rating - 5) / 5` | Explicit preference (-1 to +1) |
| Favorite | +1.0 | Strong positive interest |
| Watched | +0.5 | Moderate implicit interest |
| Watchlist | +0.7 | Intent to watch |
| Search | +0.2 | Weak interest from catalog-matched searches |

Search history alone does not enable personalization.

### Personalization threshold

Users with fewer than **3 meaningful interactions** (ratings, favorites, watched items, watchlist items) receive **cold-start** recommendations from existing discovery/popular infrastructure.

### Similarity algorithm (v1)

For movies and TV shows:

```text
similarityScore =
    genreOverlap   * 0.50 +
    castOverlap    * 0.20 +
    ratingSimilar  * 0.15 +
    yearProximity  * 0.15
```

Tie-breakers: similarity score DESC → vote count DESC → vote average DESC → title ASC.

Cast overlap degrades gracefully when cast data is unavailable.

### Personalized scoring (v1)

```text
recommendationScore =
    genrePreference     * 0.45 +
    personSimilarity    * 0.20 +
    behaviorSimilarity  * 0.20 +
    popularity          * 0.10 +
    recency             * 0.05
```

A lightweight diversity step limits same-genre dominance in ranked results.

### Exclusion policy

**Recommended For You** excludes:

- watched movies
- favorited movies and TV shows
- watchlisted movies and TV shows
- fully watched TV shows (all catalog episodes watched)

Partially watched TV shows remain eligible.

**Similar Content** never returns the source title itself.

### Home sections

**Personalized users:**

1. Recommended For You
2. Because You Watched (omitted when no suitable watched source)
3. Based On Your Favorites (omitted when user has no favorites)

**Cold-start users:**

1. Popular
2. Trending
3. Top Rated

Each section returns up to 10 items.

### Cache strategy

| Key pattern | TTL |
|-------------|-----|
| `recommendation-similar-movie:{movieId}:page:{page}:size:{size}:v1` | 30 minutes |
| `recommendation-similar-tv:{tvShowId}:page:{page}:size:{size}:v1` | 30 minutes |
| `recommendation-user:{userId}:{type}:{page}:{size}:v1` | 5 minutes |
| `recommendation-home:{userId}:v1` | 5 minutes |

TTL-based invalidation is used in this step. Personalized caches naturally refresh after user behavior changes within the TTL window.

### Configuration

```json
"Recommendations": {
  "MinimumPersonalizationInteractions": 3,
  "MaximumCandidates": 500,
  "HomeSectionItemCount": 10
}
```

All algorithm weights are configurable under `Recommendations` in `appsettings.json`.

### Known limitations

- No collaborative filtering between users
- No embeddings, vector search, or ML models
- No persisted recommendation scores or user preference tables
- Actor/crew preferences are calculated dynamically and may be limited by catalog cast coverage
- Candidate generation is bounded to `MaximumCandidates` (default 500) per request

### Future ML migration path

`IRecommendationService` is designed so a future `MLRecommendationService` can replace the current rule-based implementation while keeping the same API contracts.

## Home & Discovery API

The Home endpoint is the mobile-oriented orchestration layer that composes existing recommendation, discovery, and watch-history capabilities into a single response. It does **not** replace `/api/recommendations/home` or duplicate recommendation algorithms.

### Endpoint

| Endpoint | Auth | Notes |
|----------|------|-------|
| `GET /api/home` | JWT required | Aggregated home sections for the authenticated user |

### Query parameters

| Parameter | Default | Validation |
|-----------|---------|------------|
| `type` | `all` | `all`, `movie`, or `tv` |
| `sectionSize` | `10` | `1`–`20` |

`UserId` is always taken from the JWT (`ICurrentUser.UserId`). Never accepted from query parameters.

### Section types

`RecommendedForYou`, `BecauseYouWatched`, `BasedOnFavorites`, `ContinueWatching`, `Trending`, `Popular`, `NewReleases`, `TopRated`, `Genre`

### Personalized vs cold-start

Personalization is determined by the existing recommendation service (cold-start threshold: 3 meaningful interactions). Home does not reimplement that rule.

**Personalized users** (sections with items only):

1. Continue Watching
2. Recommended For You
3. Because You Watched
4. Based On Your Favorites
5. Trending
6. Popular
7. New Releases
8. Top Rated
9+. Configured genre sections

**Cold-start users**:

1. Continue Watching (if available)
2. Trending
3. Popular
4. New Releases
5. Top Rated
6+. Configured genre sections

Empty sections are omitted.

### Continue Watching semantics

Continue Watching comes from watch history, not recommendations.

- **TV shows:** partially watched shows with a next unwatched episode appear; fully watched shows do not.
- **Movies:** not included — playback position tracking is not implemented yet.

> **Known limitation:** Continue Watching is not true playback-position tracking yet. Movie resume support will be added in a later step.

### Discovery extensions used by Home

| Capability | Ordering |
|------------|----------|
| New Releases | `releaseDate DESC`, `title ASC`, `id ASC` (movies with release date; TV with first air date) |
| Top Rated | `voteAverage DESC`, `voteCount DESC`, `title ASC`, `id ASC` (excludes `voteCount = 0`) |
| Genre sections | Filter by configured genre name; popularity-based secondary ordering |

### Type filtering

`type=movie` returns only movies in every section. `type=tv` returns only TV shows. `type=all` may include both.

### Deduplication

Duplicate content IDs within a single section are removed. The same title may appear in multiple sections (e.g. Popular and a genre section).

### Caching

| Key pattern | TTL |
|-------------|-----|
| `home:{userId}:{type}:{sectionSize}:v1` | 5 minutes (configurable) |

Home caches the final assembled response. Lower-level recommendation and discovery caches remain independent. TTL-based invalidation is used in this step.

### Configuration

```json
"Home": {
  "DefaultSectionSize": 10,
  "MaximumSectionSize": 20,
  "CacheTtlMinutes": 5,
  "GenreSections": [
    "Science Fiction",
    "Action",
    "Drama",
    "Comedy"
  ]
}
```

Genre names are matched case-insensitively against catalog data. Sections for genres with no content are omitted.

### Example usage

```bash
curl http://localhost:5000/api/home \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"

curl "http://localhost:5000/api/home?type=movie&sectionSize=5" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

## Favorites & Watchlists

Authenticated users can favorite catalog items and manage personal watchlists. User identity always comes from the validated JWT (`ICurrentUser`); client requests never supply a `UserId`.

### Architecture

- **Domain:** `Favorite`, `Watchlist`, `WatchlistItem` aggregates with explicit invariants
- **Application:** dedicated services for favorites and watchlist operations
- **Abstractions:** `IFavoriteRepository`, `IWatchlistRepository`, `IWatchlistItemRepository`
- **Infrastructure:** EF Core tables with partial unique indexes for nullable movie/TV references
- **API:** `FavoritesController`, `WatchlistsController` (all endpoints require `[Authorize]`)

### Favorites endpoints

| Endpoint | Success | Errors |
|----------|---------|--------|
| `POST /api/favorites/movies/{movieId}` | `201 Created` / `200 OK` (idempotent) | `401`, `404` |
| `DELETE /api/favorites/movies/{movieId}` | `204 No Content` | `401` |
| `POST /api/favorites/tvshows/{tvShowId}` | `201 Created` / `200 OK` (idempotent) | `401`, `404` |
| `DELETE /api/favorites/tvshows/{tvShowId}` | `204 No Content` | `401` |
| `GET /api/favorites?page=&pageSize=` | `200 OK` | `400`, `401` |

### Watchlist endpoints

| Endpoint | Success | Errors |
|----------|---------|--------|
| `POST /api/watchlists` | `201 Created` | `400`, `401`, `409` duplicate name |
| `GET /api/watchlists` | `200 OK` | `401` |
| `GET /api/watchlists/{watchlistId}` | `200 OK` | `401`, `404` |
| `DELETE /api/watchlists/{watchlistId}` | `204 No Content` | `401`, `404` |
| `POST /api/watchlists/{id}/movies/{movieId}` | `201` / `200` (idempotent) | `401`, `404` |
| `DELETE /api/watchlists/{id}/movies/{movieId}` | `204` | `401`, `404` |
| `POST /api/watchlists/{id}/tvshows/{tvShowId}` | `201` / `200` (idempotent) | `401`, `404` |
| `DELETE /api/watchlists/{id}/tvshows/{tvShowId}` | `204` | `401`, `404` |
| `GET /api/watchlists/{id}/items?page=&pageSize=` | `200 OK` | `400`, `401`, `404` |

### Pagination

`GET /api/favorites` and `GET /api/watchlists/{id}/items` use the shared pagination defaults:

- `page` default: `1`
- `pageSize` default: `20`
- `pageSize` maximum: `100`

### Ownership & security

- Watchlist access is scoped to the authenticated user; another user's `watchlistId` returns `404 Not Found`
- Duplicate favorites/watchlist items are idempotent (`200 OK`) rather than errors
- Duplicate watchlist names per user return `409 Conflict`
- Catalog references are validated against the local database (no external provider calls)

### Example usage

```bash
# Create watchlist
curl -X POST http://localhost:5000/api/watchlists \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"Weekend Watch"}'

# Favorite a movie
curl -X POST http://localhost:5000/api/favorites/movies/MOVIE_GUID \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"

# List favorites
curl "http://localhost:5000/api/favorites?page=1&pageSize=20" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

## Ratings & Reviews

Users can rate and review movies and TV shows independently. Ratings use scores **1–10**. Reviews support up to **5000** characters.

### Rating endpoints

| Endpoint | Auth | Success | Notes |
|----------|------|---------|-------|
| `POST /api/ratings/movies/{movieId}` | JWT | `201` / `200` | Upsert (update if exists) |
| `POST /api/ratings/tvshows/{tvShowId}` | JWT | `201` / `200` | Upsert |
| `DELETE /api/ratings/movies/{movieId}` | JWT | `204` | Own rating only |
| `DELETE /api/ratings/tvshows/{tvShowId}` | JWT | `204` | Own rating only |
| `GET /api/ratings/movies/{movieId}/me` | JWT | `200` | Current user's rating |
| `GET /api/ratings/tvshows/{tvShowId}/me` | JWT | `200` | Current user's rating |
| `GET /api/ratings/movies/{movieId}` | Public | `200` | Aggregate summary |
| `GET /api/ratings/tvshows/{tvShowId}` | Public | `200` | Aggregate summary |

### Review endpoints

| Endpoint | Auth | Success | Notes |
|----------|------|---------|-------|
| `POST /api/reviews/movies/{movieId}` | JWT | `201` | `409` if duplicate |
| `POST /api/reviews/tvshows/{tvShowId}` | JWT | `201` | `409` if duplicate |
| `PUT /api/reviews/movies/{movieId}` | JWT | `200` | Own review only |
| `PUT /api/reviews/tvshows/{tvShowId}` | JWT | `200` | Own review only |
| `DELETE /api/reviews/movies/{movieId}` | JWT | `204` | Own review only |
| `DELETE /api/reviews/tvshows/{tvShowId}` | JWT | `204` | Own review only |
| `GET /api/reviews/movies/{movieId}/me` | JWT | `200` | Current user's review |
| `GET /api/reviews/tvshows/{tvShowId}/me` | JWT | `200` | Current user's review |
| `GET /api/reviews/movies/{movieId}` | Public | `200` | Paginated list |
| `GET /api/reviews/tvshows/{tvShowId}` | Public | `200` | Paginated list |

Public review lists are ordered by `created_at DESC` and use the shared pagination defaults (`page=1`, `pageSize=20`, max `100`).

### Example usage

```bash
# Rate a movie
curl -X POST http://localhost:5000/api/ratings/movies/MOVIE_GUID \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"score":9}'

# Public rating summary
curl http://localhost:5000/api/ratings/movies/MOVIE_GUID

# Create a review
curl -X POST http://localhost:5000/api/reviews/movies/MOVIE_GUID \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"content":"Excellent movie."}'

# Public review list
curl "http://localhost:5000/api/reviews/movies/MOVIE_GUID?page=1&pageSize=20"
```

## Advanced Search & Discovery

Unified local-catalog search across movies and TV shows. Provider-backed `/api/movies/search` and `/api/tvshows/search` remain unchanged; advanced search queries the PostgreSQL catalog only.

### Architecture

```text
API (SearchController, DiscoveryController, SearchHistoryController)
  ↓
Application (ISearchService, IDiscoveryService, IAutocompleteService, ISearchHistoryService)
  ↓
ISearchRepository / ISearchHistoryRepository
  ↓
PostgreSQL (EF Core) + Redis (ICacheService)
```

`ISearchRepository` is designed so a future `ElasticsearchSearchRepository` can replace `SearchRepository` without changing controllers or API contracts.

### Public endpoints

| Endpoint | Notes |
|----------|-------|
| `GET /api/search` | Unified search with filters/sort (`q`, `type`, `genreId`, `year`, `minRating`, `maxRating`, `sort`, pagination) |
| `GET /api/search/autocomplete?q=` | Max 10 suggestions from local catalog |
| `GET /api/discovery/popular` | Deterministic popularity: `voteCount DESC`, then `voteAverage DESC` |
| `GET /api/discovery/trending` | Approximation using vote metrics + release/air date recency |

### Authenticated endpoints

| Endpoint | Notes |
|----------|-------|
| `GET /api/search/history` | Personal search history (`SearchedAt DESC`) |
| `DELETE /api/search/history` | Clear all history for current user |
| `DELETE /api/search/history/{id}` | Delete single owned entry |

### Filters & sorting

- **type:** `movie`, `tv`, `all` (default `all`)
- **sort:** `relevance`, `rating`, `rating_desc`, `rating_asc`, `date_desc`, `date_asc`, `title_asc`, `title_desc`, `popular`
- **Relevance:** exact title match → prefix match → contains, then vote average/count

### Redis caching (TTL-only invalidation)

| Key pattern | TTL |
|-------------|-----|
| `search:{normalized-request}` | 5 min |
| `search-autocomplete:{query}` | 10 min |
| `discovery-popular:{type}:{page}:{size}` | 10 min |
| `discovery-trending:{type}:{page}:{size}` | 5 min |

Search history is never cached.

### Search history behavior

When an authenticated user searches with a valid `q` (≥2 chars), the normalized query is recorded. If the user's most recent history entry has the same normalized query, `SearchedAt` is updated instead of inserting a duplicate.

### Current limitations

- Text matching uses PostgreSQL `ILIKE '%query%'` (case-insensitive contains). No fuzzy/stemming yet.
- Trending is a catalog-based approximation, not real user-behavior analytics.
- Unified `type=all` pagination uses EF `Concat` over projected movie/TV queries in PostgreSQL.

## Watch History

Movies use binary watched/not-watched state. TV shows are tracked at the **episode level** only (no whole-show watched record).

All endpoints require JWT authentication (`[Authorize]`). `UserId` is always taken from the authenticated user — never from the request.

### Movie endpoints

| Endpoint | Success | Notes |
|----------|---------|-------|
| `POST /api/watch-history/movies/{movieId}` | `201` / `200` | Upsert; updates `watchedAt` on repeat |
| `DELETE /api/watch-history/movies/{movieId}` | `204` | Idempotent |
| `GET /api/watch-history/movies/{movieId}/me` | `200` | Returns `isWatched: false` when unwatched |
| `GET /api/watch-history/movies?page=&pageSize=` | `200` | Ordered by `watched_at DESC` |

### Episode endpoints

| Endpoint | Success | Notes |
|----------|---------|-------|
| `POST /api/watch-history/episodes/{episodeId}` | `201` / `200` | Upsert; updates `watchedAt` on repeat |
| `DELETE /api/watch-history/episodes/{episodeId}` | `204` | Idempotent |
| `GET /api/watch-history/episodes/{episodeId}/me` | `200` | Returns `isWatched: false` when unwatched |
| `GET /api/watch-history/episodes?page=&pageSize=` | `200` | Includes TV show/season metadata |

### Recent history & progress

| Endpoint | Success | Notes |
|----------|---------|-------|
| `GET /api/watch-history/recent?page=&pageSize=` | `200` | Movies and episodes merged by `watched_at` |
| `GET /api/watch-history/tvshows/{tvShowId}` | `200` | Total/watched counts, %, next unwatched episode |
| `GET /api/watch-history/tvshows/{tvShowId}/seasons/{seasonNumber}` | `200` | Season-level progress and next episode |

Pagination defaults: `page=1`, `pageSize=20`, max `pageSize=100`.

### Example usage

```bash
# Mark movie as watched
curl -X POST http://localhost:5000/api/watch-history/movies/MOVIE_GUID \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"

# Check watch status
curl http://localhost:5000/api/watch-history/movies/MOVIE_GUID/me \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"

# Mark episode as watched
curl -X POST http://localhost:5000/api/watch-history/episodes/EPISODE_GUID \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"

# TV show progress
curl http://localhost:5000/api/watch-history/tvshows/TVSHOW_GUID \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

## Run Tests

```bash
dotnet test
```

Run a specific project:

```bash
dotnet test tests/MovieApp.UnitTests
dotnet test tests/MovieApp.IntegrationTests
```

## Dependency Injection

Composition is registered through extension methods:

- `AddApplication()` in `MovieApp.Application`
- `AddInfrastructure(configuration)` in `MovieApp.Infrastructure`
- `AddApi(configuration)` in `MovieApp.Api`

## Logging

Structured logging is configured with Serilog and reads settings from configuration files.
