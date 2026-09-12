# MovieApp Mobile API Context

Reference for consuming the existing MovieApp REST API from a React Native + Expo + TypeScript mobile client. All routes, DTOs, and behaviors documented here are taken from the current backend implementation.

---

## 1. Backend Overview

| Item | Value |
|------|-------|
| **.NET version** | .NET 10 (`net10.0`) |
| **API project** | `MovieApp.Api` (`src/MovieApp.Api/`) |
| **Development base URL** | `http://localhost:5027` (HTTP profile) |
| **Development HTTPS URL** | `https://localhost:7024` |
| **Swagger UI** | `GET /swagger` (Development environment only) |

### Authentication mechanism

JWT Bearer tokens (HMAC-SHA256). Configuration section: `Authentication:Jwt`.

| Setting | Default |
|---------|---------|
| Issuer | `MovieApp` |
| Audience | `MovieApp.Mobile` |
| Access token lifetime | 60 minutes (`AccessTokenMinutes`) |

JWT claims include `sub` (user ID), `email`, `jti`, `iat`, and `security_stamp`. The security stamp is rotated on email/password change and invalidates previously issued tokens.

### JSON conventions

- Property names are **camelCase** (ASP.NET Core default `System.Text.Json`).
- Request bodies use `Content-Type: application/json`.
- `record` types in C# contracts map directly to JSON objects.

### Date/time conventions

| Type | Serialization |
|------|---------------|
| `DateOnly?` | ISO 8601 date string: `"2024-03-15"` |
| `DateTime` / `DateTime?` | ISO 8601 with UTC offset, e.g. `"2026-09-11T14:30:00Z"` |
| `DateTimeOffset` | ISO 8601 with offset |

All server-side timestamps are stored and returned in UTC.

### Pagination conventions

Shared across paginated endpoints (see [Section 16](#16-pagination)):

| Parameter | Default | Constraints |
|-----------|---------|-------------|
| `page` | `1` | Must be ≥ 1 |
| `pageSize` | `20` | Must be 1–100 |

### Common HTTP status codes

| Code | Meaning |
|------|---------|
| `200` | Success |
| `201` | Created |
| `204` | Success, no body |
| `400` | Validation error |
| `401` | Authentication required or failed |
| `404` | Resource not found |
| `409` | Conflict (duplicate resource) |

### Common error response format

Controllers return ASP.NET `ProblemDetails` for error responses:

```json
{
  "status": 400,
  "title": "Invalid search request.",
  "detail": "Search query must be at least 2 characters."
}
```

There is no global exception middleware. Unhandled server errors may return the framework default `500` response; there is no custom standardized `500` envelope in the application layer.

---

## 2. Authentication

> **Important:** The mobile client must **never** send a `userId` (or any user identifier) in request bodies or query strings for authenticated operations. The backend derives the current user exclusively from the JWT `sub` claim.

### `POST /api/auth/register`

**Auth:** None

**Request body** (`RegisterRequest`):

```json
{
  "email": "user@example.com",
  "password": "securepassword",
  "displayName": "Emre"
}
```

**Validation:**

| Field | Rules |
|-------|-------|
| `email` | Required, valid email format, max 320 characters |
| `password` | Required, 8–128 characters |
| `displayName` | Required, max 100 characters (trimmed) |

**Response `201 Created`** (`AuthResponse`):

```json
{
  "accessToken": "<jwt>",
  "expiresAt": "2026-09-11T15:30:00Z",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "user@example.com",
    "userName": "user",
    "displayName": "Emre",
    "createdAt": "2026-09-11T14:30:00Z"
  }
}
```

`userName` is auto-generated from the email address at registration; it is not set by the client.

**Error responses:**

| Status | Title | When |
|--------|-------|------|
| `400` | Invalid registration request. | Validation failure |
| `409` | Registration conflict. | Email already exists (`"A user with this email address already exists."`) |

---

### `POST /api/auth/login`

**Auth:** None

**Request body** (`LoginRequest`):

```json
{
  "email": "user@example.com",
  "password": "securepassword"
}
```

**Validation:**

| Field | Rules |
|-------|-------|
| `email` | Required |
| `password` | Required, max 128 characters |

**Response `200 OK`** (`AuthResponse`): Same shape as register.

**Error responses:**

| Status | Title | When |
|--------|-------|------|
| `400` | Invalid login request. | Validation failure |
| `401` | Authentication failed. | Invalid credentials (`"Invalid email or password."`) |

---

### `GET /api/auth/me`

**Auth:** Bearer JWT required

**Response `200 OK`** (`CurrentUserResponse`):

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "userName": "user",
  "displayName": "Emre",
  "createdAt": "2026-09-11T14:30:00Z"
}
```

**Error responses:** `401` (missing/invalid token), `404` (user not found)

---

### JWT behavior

| Item | Value |
|------|-------|
| Header format | `Authorization: Bearer <accessToken>` |
| Token expiration | 60 minutes from issuance (configurable via `AccessTokenMinutes`) |
| Token refresh | No refresh-token endpoint exists; re-login or re-authenticate via email/password change (which returns a new token) |
| Logout | **No server-side logout endpoint.** Discard the token locally. |

### Identifying authenticated endpoints

Endpoints requiring JWT are marked with `[Authorize]` on the controller or action. Unauthenticated requests to these endpoints return `401`.

---

## 3. User / Profile

All endpoints require Bearer JWT. The controller has class-level `[Authorize]`. User identity is always derived from the JWT — never send `userId`.

### `GET /api/users/me`

Returns the current user's profile.

**Response `200 OK`** (`UserProfileResponse`):

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "userName": "user",
  "displayName": "Emre",
  "createdAt": "2026-09-11T14:30:00Z"
}
```

**Status codes:** `200`, `401`, `404`

---

### `PUT /api/users/me/profile`

Updates display name only.

**Request body** (`UpdateProfileRequest`):

```json
{
  "displayName": "New Name"
}
```

**Validation:** `displayName` required, max 100 characters.

**Response `200 OK`:** `UserProfileResponse`

**Status codes:** `200`, `400`, `401`, `404`

---

### `PUT /api/users/me/email`

**Request body** (`ChangeEmailRequest`):

```json
{
  "email": "newemail@example.com",
  "currentPassword": "currentpassword"
}
```

**Validation:** Valid email format (max 320 chars), `currentPassword` required.

**Response `200 OK`** (`UserProfileAuthResponse`):

```json
{
  "accessToken": "<new-jwt>",
  "expiresAt": "2026-09-11T16:30:00Z",
  "user": { /* UserProfileResponse */ }
}
```

A new JWT is issued because the security stamp is rotated.

**Status codes:** `200`, `400`, `401`, `404`, `409` (email already in use)

---

### `PUT /api/users/me/password`

**Request body** (`ChangePasswordRequest`):

```json
{
  "currentPassword": "currentpassword",
  "newPassword": "newsecurepassword"
}
```

**Validation:**

| Field | Rules |
|-------|-------|
| `currentPassword` | Required |
| `newPassword` | 8–128 characters, must differ from current password |

**Response `200 OK`:** `UserProfileAuthResponse` (new JWT issued)

**Status codes:** `200`, `400`, `401`, `404`

---

### `GET /api/users/me/statistics`

**Response `200 OK`** (`UserStatisticsResponse`):

```json
{
  "favoriteMovieCount": 5,
  "favoriteTvShowCount": 3,
  "watchlistCount": 2,
  "watchlistItemCount": 12,
  "ratedMovieCount": 8,
  "ratedTvShowCount": 4,
  "reviewedMovieCount": 2,
  "reviewedTvShowCount": 1,
  "watchedMovieCount": 15,
  "watchedEpisodeCount": 42,
  "totalRatingCount": 12,
  "totalReviewCount": 3,
  "totalWatchedCount": 57
}
```

**Status codes:** `200`, `401`

---

### `DELETE /api/users/me`

Permanently deletes the account and cascades deletion of all user-owned data (favorites, watchlists, ratings, reviews, watch history, search history). Catalog data is never deleted.

**Request body** (`DeleteAccountRequest`):

```json
{
  "currentPassword": "currentpassword"
}
```

**Response:** `204 No Content`

**Status codes:** `204`, `400`, `401`, `404`

---

## 4. Movies

### `GET /api/movies/search`

**Auth:** None

**Query parameters:**

| Param | Type | Default | Notes |
|-------|------|---------|-------|
| `q` | string | `""` | **Required** (min 2, max 100 characters after trim) |
| `page` | int | `1` | ≥ 1 |
| `pageSize` | int | `20` | 1–100 |

Provider-backed search (not the local catalog).

**Response `200 OK`** (`MovieSearchResponse`):

```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "externalIds": { "tmdbId": 157336, "tvdbId": null, "imdbId": "tt0816692" },
      "title": "Interstellar",
      "overview": "...",
      "releaseDate": "2014-11-07",
      "posterPath": "/path.jpg",
      "voteAverage": 8.4,
      "voteCount": 32000
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

**Status codes:** `200`, `400`

---

### `GET /api/movies/{id}`

**Auth:** None

**Route parameter:** `id` — movie GUID

**Response `200 OK`** (`MovieDetailsResponse`):

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "externalIds": { "tmdbId": 157336, "tvdbId": null, "imdbId": "tt0816692" },
  "title": "Interstellar",
  "originalTitle": "Interstellar",
  "overview": "...",
  "releaseDate": "2014-11-07",
  "runtimeMinutes": 169,
  "posterPath": "/path.jpg",
  "backdropPath": "/backdrop.jpg",
  "originalLanguage": "en",
  "voteAverage": 8.4,
  "voteCount": 32000,
  "genres": ["Adventure", "Drama", "Science Fiction"]
}
```

**Status codes:** `200`, `404`

---

## 5. TV Shows

Provider-backed TV catalog endpoints hydrate from the configured catalog provider (`MovieProviders:Provider`):

- **`Fake`** (default in local/testing): deterministic development data
- **`Tmdb`**: real TMDB TV search and lazy season/episode hydration

Public API `{id}` values are always internal MovieApp GUIDs. TMDB IDs appear only in `externalIds.tmdbId` after persistence. Provider external IDs use the format `tmdb-{tmdbId}` internally and are never exposed directly to clients.

When `Provider=Tmdb`, TV search uses TMDB pagination (fixed page size of 20). Search responses are cached for 15 minutes.

### `GET /api/tvshows/search`

**Auth:** None

**Query parameters:** Same as movie search (`q`, `page`, `pageSize`).

**Response `200 OK`** (`TvShowSearchResponse`):

```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "externalIds": { "tmdbId": 1396, "tvdbId": 81189, "imdbId": "tt0903747" },
      "title": "Breaking Bad",
      "originalTitle": "Breaking Bad",
      "overview": "...",
      "firstAirDate": "2008-01-20",
      "posterPath": "/path.jpg",
      "backdropPath": "/backdrop.jpg",
      "originalLanguage": "en",
      "voteAverage": 8.9,
      "voteCount": 12000
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

**Status codes:** `200`, `400`

---

### `GET /api/tvshows/{id}`

**Auth:** None

**Response `200 OK`** (`TvShowDetailsResponse`):

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "externalIds": { "tmdbId": 1396, "tvdbId": 81189, "imdbId": "tt0903747" },
  "title": "Breaking Bad",
  "originalTitle": "Breaking Bad",
  "overview": "...",
  "firstAirDate": "2008-01-20",
  "lastAirDate": "2013-09-29",
  "posterPath": "/path.jpg",
  "backdropPath": "/backdrop.jpg",
  "originalLanguage": "en",
  "voteAverage": 8.9,
  "voteCount": 12000,
  "status": "Ended",
  "genres": ["Crime", "Drama", "Thriller"],
  "seasons": [
    {
      "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "seasonNumber": 1,
      "name": "Season 1",
      "airDate": "2008-01-20",
      "episodeCount": 7,
      "posterPath": "/s1.jpg"
    }
  ]
}
```

`status` values: `"Returning Series"`, `"In Production"`, `"Ended"`, `"Canceled"`, `"Pilot"`, `"Planned"`.

**Status codes:** `200`, `404`

---

### `GET /api/tvshows/{id}/seasons/{seasonNumber}`

**Auth:** None

**Route parameters:**

| Param | Type | Validation |
|-------|------|------------|
| `id` | GUID | TV show ID |
| `seasonNumber` | int | Must be ≥ 1 |

**Response `200 OK`** (`SeasonResponse`):

```json
{
  "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "tvShowId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "seasonNumber": 1,
  "name": "Season 1",
  "overview": "...",
  "airDate": "2008-01-20",
  "episodeCount": 7,
  "posterPath": "/s1.jpg",
  "episodes": [
    {
      "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "episodeNumber": 1,
      "name": "Pilot",
      "airDate": "2008-01-20",
      "runtimeMinutes": 58,
      "stillPath": "/still.jpg",
      "voteAverage": 8.2,
      "voteCount": 2100
    }
  ]
}
```

**Status codes:** `200`, `400`, `404`

---

### `GET /api/tvshows/{id}/seasons/{seasonNumber}/episodes/{episodeNumber}`

**Auth:** None

**Route parameters:**

| Param | Type | Validation |
|-------|------|------------|
| `id` | GUID | TV show ID |
| `seasonNumber` | int | Must be ≥ 1 |
| `episodeNumber` | int | Must be ≥ 1 |

**Response `200 OK`** (`EpisodeResponse`):

```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "tvShowId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "seasonId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "seasonNumber": 1,
  "episodeNumber": 1,
  "name": "Pilot",
  "overview": "...",
  "airDate": "2008-01-20",
  "runtimeMinutes": 58,
  "stillPath": "/still.jpg",
  "voteAverage": 8.2,
  "voteCount": 2100
}
```

**Status codes:** `200`, `400`, `404`

---

## 6. Unified Search

Queries the **local PostgreSQL catalog** (not external providers). Provider-backed `/api/movies/search` and `/api/tvshows/search` remain separate.

### `GET /api/search`

**Auth:** None (search history is recorded only when authenticated)

**Query parameters:**

| Param | Type | Default | Notes |
|-------|------|---------|-------|
| `q` | string | optional | Min 2, max 100 characters (when provided) |
| `type` | string | `all` | `movie`, `tv`, or `all` (case-insensitive) |
| `genreId` | GUID | optional | Filter by genre ID |
| `year` | int | optional | 1888 to (current year + 1) |
| `minRating` | decimal | optional | 0.0–10.0 |
| `maxRating` | decimal | optional | 0.0–10.0; must be ≥ `minRating` |
| `sort` | string | `relevance` | See sort values below |
| `page` | int | `1` | ≥ 1 |
| `pageSize` | int | `20` | 1–100 |

**Sort values** (case-insensitive): `relevance`, `rating`, `rating_desc`, `rating_asc`, `date_desc`, `date_asc`, `title_asc`, `title_desc`, `popular`

**Response `200 OK`** (`SearchResponse`):

```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "type": "movie",
      "title": "Interstellar",
      "originalTitle": "Interstellar",
      "overview": "...",
      "posterUrl": "/path.jpg",
      "backdropUrl": "/backdrop.jpg",
      "releaseDate": "2014-11-07",
      "voteAverage": 8.4,
      "voteCount": 32000,
      "year": 2014
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

**Status codes:** `200`, `400`

---

### `GET /api/search/autocomplete`

**Auth:** None

**Query parameters:**

| Param | Type | Required | Notes |
|-------|------|----------|-------|
| `q` | string | Yes | Min 2, max 100 characters |

Returns a maximum of **10** suggestions.

**Response `200 OK`** (`SearchAutocompleteResponse`):

```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "type": "movie",
      "title": "Interstellar"
    }
  ]
}
```

**Status codes:** `200`, `400`

---

## 7. Search History

All endpoints require Bearer JWT.

### `GET /api/search/history`

**Query parameters:** `page` (default `1`), `pageSize` (default `20`)

**Response `200 OK`** (`SearchHistoryResponse`):

```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "query": "interstellar",
      "searchedAt": "2026-09-11T14:30:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

Ordered by `searchedAt DESC`.

**Status codes:** `200`, `400`, `401`

---

### `DELETE /api/search/history`

Clears all search history for the current user.

**Response:** `204 No Content`

**Status codes:** `204`, `401`

---

### `DELETE /api/search/history/{id}`

Deletes a single owned history entry.

**Route parameter:** `id` — history entry GUID

**Response:** `204 No Content`

**Status codes:** `204`, `401`, `404`

---

## 8. Discovery

### Public discovery endpoints

Only two discovery routes are exposed as standalone API endpoints:

#### `GET /api/discovery/popular`

**Auth:** None

**Query parameters:**

| Param | Type | Default |
|-------|------|---------|
| `page` | int | `1` |
| `pageSize` | int | `20` |
| `type` | string | `all` |

Ordering: `voteCount DESC`, then `voteAverage DESC`.

**Response:** `SearchResponse` (same shape as unified search)

**Status codes:** `200`, `400`

---

#### `GET /api/discovery/trending`

**Auth:** None

**Query parameters:** Same as popular.

Approximation using vote metrics and release/air date recency.

**Response:** `SearchResponse`

**Status codes:** `200`, `400`

---

### Discovery capabilities available only via Home API

The following discovery capabilities are **not** exposed as standalone `/api/discovery/*` endpoints. They appear as sections within `GET /api/home`:

| Capability | Section type in Home response |
|------------|-------------------------------|
| New Releases | `NewReleases` |
| Top Rated | `TopRated` |
| Genre sections | `Genre` (configured genre names from `Home:GenreSections`) |

Default configured genres: `"Science Fiction"`, `"Action"`, `"Drama"`, `"Comedy"`.

---

## 9. Home

### `GET /api/home`

**Auth:** Bearer JWT required

Aggregated home screen sections composing recommendations, discovery, and watch history.

**Query parameters:**

| Param | Type | Default | Constraints |
|-------|------|---------|-------------|
| `type` | string | `all` | `movie`, `tv`, or `all` |
| `sectionSize` | int | `10` | 1–20 (max from `Home:MaximumSectionSize`) |

**Response `200 OK`** (`HomeResponse`):

```json
{
  "sections": [
    {
      "type": "ContinueWatching",
      "title": "Continue Watching",
      "items": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "contentType": "tv",
          "title": "Breaking Bad",
          "originalTitle": "Breaking Bad",
          "posterUrl": "/path.jpg",
          "backdropUrl": "/backdrop.jpg",
          "releaseDate": "2008-01-20",
          "voteAverage": 8.9,
          "voteCount": 12000
        }
      ],
      "displayOrder": 1
    }
  ],
  "isPersonalized": true
}
```

### Section types

`type` values are PascalCase strings from `HomeSectionType.ToString()`:

| Type | Title (typical) |
|------|-----------------|
| `RecommendedForYou` | Recommended For You |
| `BecauseYouWatched` | Because You Watched |
| `BasedOnFavorites` | Based On Favorites |
| `ContinueWatching` | Continue Watching |
| `Trending` | Trending |
| `Popular` | Popular |
| `NewReleases` | New Releases |
| `TopRated` | Top Rated |
| `Genre` | Genre name (e.g. `"Science Fiction"`) |

### Personalized vs cold-start behavior

| Condition | Behavior |
|-----------|----------|
| User has ≥ 3 meaningful interactions (ratings, favorites, watched items, watchlist items) | `isPersonalized: true`; includes recommendation sections |
| Fewer than 3 interactions | `isPersonalized: false`; cold-start sections only |

**Personalized section order:** ContinueWatching → RecommendedForYou → BecauseYouWatched → BasedOnFavorites → Trending → Popular → NewReleases → TopRated → Genre sections

**Cold-start section order:** ContinueWatching → Trending → Popular → NewReleases → TopRated → Genre sections

Empty sections are omitted from the response.

### Continue Watching semantics

- Sourced from watch history, not recommendations.
- **TV shows:** Partially watched shows (with a next unwatched episode) appear. Fully watched shows do not.
- **Movies:** Not included. The Continue Watching section is empty when `type=movie`.

> **Known limitation:** There is no playback-position tracking. Continue Watching reflects episode-level watch state, not resume timestamps. Movie resume support is not implemented.

### Type filtering

`type=movie` returns only movies in every section. `type=tv` returns only TV shows. `type=all` may include both.

### Deduplication

Duplicate content IDs within a single section are removed. The same title may appear in multiple sections.

### Caching

Home responses are cached per user/type/sectionSize (default TTL: 5 minutes).

**Status codes:** `200`, `400`, `401`

---

## 10. Recommendations

### Similar content (public)

#### `GET /api/recommendations/movies/{movieId}/similar`

**Auth:** None

**Query parameters:** `page` (default `1`), `pageSize` (default `20`)

**Response `200 OK`** (`RecommendationResponse`):

```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "type": "movie",
      "title": "Inception",
      "originalTitle": "Inception",
      "overview": "...",
      "posterUrl": "/path.jpg",
      "backdropUrl": "/backdrop.jpg",
      "releaseDate": "2010-07-16",
      "voteAverage": 8.4,
      "voteCount": 28000,
      "year": 2010,
      "score": 0.85,
      "reason": "Similar genres and cast"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 10,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

**Status codes:** `200`, `400`, `404`

---

#### `GET /api/recommendations/tvshows/{tvShowId}/similar`

Same as similar movies but for TV shows.

**Status codes:** `200`, `400`, `404`

---

### Personalized recommendations (authenticated)

#### `GET /api/recommendations`

**Auth:** Bearer JWT required

**Query parameters:**

| Param | Type | Default |
|-------|------|---------|
| `page` | int | `1` |
| `pageSize` | int | `20` |
| `type` | string | `all` |

**Response:** `RecommendationResponse`

Users with fewer than 3 meaningful interactions receive cold-start recommendations (popular content).

**Status codes:** `200`, `400`, `401`

---

#### `GET /api/recommendations/home`

**Auth:** Bearer JWT required

Returns recommendation sections only (not the full home orchestration).

**Response `200 OK`** (`RecommendationHomeResponse`):

```json
{
  "sections": [
    {
      "key": "recommended-for-you",
      "title": "Recommended For You",
      "items": [ /* RecommendationItemResponse[] */ ]
    }
  ]
}
```

**Section keys:**

| Key | Title |
|-----|-------|
| `recommended-for-you` | Recommended For You |
| `because-you-watched` | Because You Watched |
| `similar-to-favorites` | Based On Your Favorites |
| `popular` | Popular (cold-start) |
| `trending` | Trending (cold-start) |
| `top-rated` | Top Rated (cold-start) |

**Status codes:** `200`, `401`

---

### Distinction: similar vs personalized vs Home sections

| API | Purpose |
|-----|---------|
| **Similar** (`/similar`) | Content-based similarity to a specific movie/TV show. Public, no user context. |
| **Personalized** (`/api/recommendations`) | User-behavior-driven paginated list. Requires auth. |
| **Recommendation Home** (`/api/recommendations/home`) | Grouped recommendation sections only. Requires auth. |
| **Home** (`/api/home`) | Full mobile home screen: recommendations + discovery + Continue Watching + genre sections. Requires auth. |

`GET /api/home` does not replace `GET /api/recommendations/home`; they serve different scopes.

---

## 11. Favorites

All endpoints require Bearer JWT.

### `POST /api/favorites/movies/{movieId}`

**Response:** `201 Created` (new favorite) or `200 OK` (already favorited, idempotent)

**Status codes:** `200`, `201`, `401`, `404`

---

### `DELETE /api/favorites/movies/{movieId}`

**Response:** `204 No Content`

**Status codes:** `204`, `401`

---

### `POST /api/favorites/tvshows/{tvShowId}`

Same semantics as movie favorite.

**Status codes:** `200`, `201`, `401`, `404`

---

### `DELETE /api/favorites/tvshows/{tvShowId}`

**Response:** `204 No Content`

**Status codes:** `204`, `401`

---

### `GET /api/favorites`

**Query parameters:** `page` (default `1`), `pageSize` (default `20`)

**Response `200 OK`** (`FavoritesResponse`):

```json
{
  "movies": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "title": "Interstellar",
      "posterPath": "/path.jpg",
      "releaseDate": "2014-11-07",
      "voteAverage": 8.4
    }
  ],
  "tvShows": [
    {
      "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "title": "Breaking Bad",
      "posterPath": "/path.jpg",
      "firstAirDate": "2008-01-20",
      "voteAverage": 8.9
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 2,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

Pagination applies to the combined favorites list (movies and TV shows merged, ordered by creation date). Each page may contain a mix of both types.

**Status codes:** `200`, `400`, `401`

---

## 12. Watchlists

All endpoints require Bearer JWT.

### `POST /api/watchlists`

**Request body** (`CreateWatchlistRequest`):

```json
{
  "name": "My Watchlist"
}
```

**Validation:** Name required, max 100 characters. Duplicate names per user return `409`.

**Response `201 Created`** (`WatchlistSummaryResponse`):

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "My Watchlist",
  "createdAt": "2026-09-11T14:30:00Z",
  "updatedAt": "2026-09-11T14:30:00Z",
  "itemCount": 0
}
```

**Status codes:** `201`, `400`, `401`, `409` (`"A watchlist with this name already exists."`)

---

### `GET /api/watchlists`

**Response `200 OK`:** `WatchlistSummaryResponse[]`

**Status codes:** `200`, `401`

---

### `GET /api/watchlists/{watchlistId}`

**Response `200 OK`** (`WatchlistDetailResponse`):

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "My Watchlist",
  "createdAt": "2026-09-11T14:30:00Z",
  "updatedAt": "2026-09-11T14:30:00Z",
  "movies": [ /* WatchlistMovieItemResponse[] */ ],
  "tvShows": [ /* WatchlistTvShowItemResponse[] */ ]
}
```

Returns all items (not paginated).

**Status codes:** `200`, `401`, `404`

---

### `DELETE /api/watchlists/{watchlistId}`

**Response:** `204 No Content`

**Status codes:** `204`, `401`, `404`

---

### `POST /api/watchlists/{watchlistId}/movies/{movieId}`

**Response:** `201 Created` (new) or `200 OK` (already present, idempotent)

**Status codes:** `200`, `201`, `401`, `404`

---

### `DELETE /api/watchlists/{watchlistId}/movies/{movieId}`

**Response:** `204 No Content`

**Status codes:** `204`, `401`, `404`

---

### `POST /api/watchlists/{watchlistId}/tvshows/{tvShowId}`

Same semantics as add movie.

**Status codes:** `200`, `201`, `401`, `404`

---

### `DELETE /api/watchlists/{watchlistId}/tvshows/{tvShowId}`

**Response:** `204 No Content`

**Status codes:** `204`, `401`, `404`

---

### `GET /api/watchlists/{watchlistId}/items`

**Query parameters:** `page` (default `1`), `pageSize` (default `20`)

**Response `200 OK`** (`WatchlistItemsResponse`):

```json
{
  "movies": [ /* WatchlistMovieItemResponse[] */ ],
  "tvShows": [ /* WatchlistTvShowItemResponse[] */ ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 5,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

Each item includes `createdAt` (when added to the watchlist).

**Status codes:** `200`, `400`, `401`, `404`

---

## 13. Ratings

### Authenticated endpoints

#### `POST /api/ratings/movies/{movieId}`

**Request body** (`CreateRatingRequest`):

```json
{
  "score": 8
}
```

**Validation:** Score must be 1–10 (integer).

**Response:** `201 Created` (new) or `200 OK` (updated) — `RatingResponse`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "movieId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "tvShowId": null,
  "score": 8,
  "createdAt": "2026-09-11T14:30:00Z",
  "updatedAt": "2026-09-11T14:30:00Z"
}
```

**Status codes:** `200`, `201`, `400`, `401`, `404`

---

#### `POST /api/ratings/tvshows/{tvShowId}`

Same as movie rating.

**Status codes:** `200`, `201`, `400`, `401`, `404`

---

#### `DELETE /api/ratings/movies/{movieId}`

**Response:** `204 No Content`

**Status codes:** `204`, `401`, `404`

---

#### `DELETE /api/ratings/tvshows/{tvShowId}`

**Response:** `204 No Content`

**Status codes:** `204`, `401`, `404`

---

#### `GET /api/ratings/movies/{movieId}/me`

**Response `200 OK`:** `RatingResponse`

**Status codes:** `200`, `401`, `404`

---

#### `GET /api/ratings/tvshows/{tvShowId}/me`

**Response `200 OK`:** `RatingResponse`

**Status codes:** `200`, `401`, `404`

---

### Public aggregate endpoints

#### `GET /api/ratings/movies/{movieId}`

**Auth:** None

**Response `200 OK`** (`RatingSummaryResponse`):

```json
{
  "averageScore": 7.5,
  "ratingCount": 42,
  "scoreDistribution": {
    "1": 0,
    "2": 1,
    "3": 2,
    "4": 3,
    "5": 5,
    "6": 8,
    "7": 10,
    "8": 7,
    "9": 4,
    "10": 2
  }
}
```

**Status codes:** `200`, `404`

---

#### `GET /api/ratings/tvshows/{tvShowId}`

Same as movie aggregate.

**Status codes:** `200`, `404`

---

## 14. Reviews

### Authenticated endpoints

#### `POST /api/reviews/movies/{movieId}`

**Request body** (`CreateReviewRequest`):

```json
{
  "content": "An incredible film..."
}
```

**Validation:** Content required, max 5000 characters.

**Response `201 Created`** (`ReviewResponse`):

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "user": {
    "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "displayName": "Emre"
  },
  "content": "An incredible film...",
  "createdAt": "2026-09-11T14:30:00Z",
  "updatedAt": "2026-09-11T14:30:00Z"
}
```

**Duplicate behavior:** One review per user per movie. Returns `409` (`"A review for this movie already exists."`).

**Status codes:** `201`, `400`, `401`, `404`, `409`

---

#### `POST /api/reviews/tvshows/{tvShowId}`

Same as movie review.

**Status codes:** `201`, `400`, `401`, `404`, `409`

---

#### `PUT /api/reviews/movies/{movieId}`

**Request body** (`UpdateReviewRequest`):

```json
{
  "content": "Updated review text..."
}
```

**Response `200 OK`:** `ReviewResponse`

**Status codes:** `200`, `400`, `401`, `404`

---

#### `PUT /api/reviews/tvshows/{tvShowId}`

Same as movie update.

**Status codes:** `200`, `400`, `401`, `404`

---

#### `DELETE /api/reviews/movies/{movieId}`

**Response:** `204 No Content`

**Status codes:** `204`, `401`, `404`

---

#### `DELETE /api/reviews/tvshows/{tvShowId}`

**Response:** `204 No Content`

**Status codes:** `204`, `401`, `404`

---

#### `GET /api/reviews/movies/{movieId}/me`

**Response `200 OK`:** `ReviewResponse`

**Status codes:** `200`, `401`, `404`

---

#### `GET /api/reviews/tvshows/{tvShowId}/me`

**Response `200 OK`:** `ReviewResponse`

**Status codes:** `200`, `401`, `404`

---

### Public paginated reviews

#### `GET /api/reviews/movies/{movieId}`

**Auth:** None

**Query parameters:** `page` (default `1`), `pageSize` (default `20`)

**Response `200 OK`** (`ReviewListResponse`):

```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "user": {
        "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
        "displayName": "Emre"
      },
      "content": "An incredible film...",
      "createdAt": "2026-09-11T14:30:00Z",
      "updatedAt": "2026-09-11T14:30:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

Public author information exposes only `id` and `displayName` (no email).

**Status codes:** `200`, `400`, `404`

---

#### `GET /api/reviews/tvshows/{tvShowId}`

Same as movie reviews.

**Status codes:** `200`, `400`, `404`

---

## 15. Watch History

All endpoints require Bearer JWT.

> Movies have **binary watched state** (watched or not). TV tracking is **episode-level**. There is **no playback-position tracking** (no resume timestamps or progress percentages for movies).

### Movies

#### `POST /api/watch-history/movies/{movieId}`

Marks a movie as watched. Upsert: repeat calls update `watchedAt`.

**Response:** `201 Created` (new) or `200 OK` (already watched) — `WatchMovieResponse`:

```json
{
  "movieId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "watchedAt": "2026-09-11T14:30:00Z"
}
```

**Status codes:** `200`, `201`, `401`, `404`

---

#### `DELETE /api/watch-history/movies/{movieId}`

Unmarks a movie. Idempotent (no error if not watched).

**Response:** `204 No Content`

**Status codes:** `204`, `401`

---

#### `GET /api/watch-history/movies/{movieId}/me`

**Response `200 OK`** (`MovieWatchStatusResponse`):

```json
{
  "movieId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "isWatched": true,
  "watchedAt": "2026-09-11T14:30:00Z"
}
```

Returns `isWatched: false` with `watchedAt: null` when unwatched.

**Status codes:** `200`, `401`

---

#### `GET /api/watch-history/movies`

**Query parameters:** `page` (default `1`), `pageSize` (default `20`)

**Response `200 OK`** (`WatchedMoviesListResponse`):

```json
{
  "items": [
    {
      "movieId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "title": "Interstellar",
      "watchedAt": "2026-09-11T14:30:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

Ordered by `watchedAt DESC`.

**Status codes:** `200`, `400`, `401`

---

### Episodes

#### `POST /api/watch-history/episodes/{episodeId}`

Marks an episode as watched. Upsert semantics.

**Response:** `201 Created` or `200 OK` — `WatchEpisodeResponse`:

```json
{
  "episodeId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "watchedAt": "2026-09-11T14:30:00Z"
}
```

**Status codes:** `200`, `201`, `401`, `404`

---

#### `DELETE /api/watch-history/episodes/{episodeId}`

**Response:** `204 No Content`

**Status codes:** `204`, `401`

---

#### `GET /api/watch-history/episodes/{episodeId}/me`

**Response `200 OK`** (`EpisodeWatchStatusResponse`):

```json
{
  "episodeId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "isWatched": true,
  "watchedAt": "2026-09-11T14:30:00Z"
}
```

**Status codes:** `200`, `401`

---

#### `GET /api/watch-history/episodes`

**Query parameters:** `page`, `pageSize`

**Response `200 OK`** (`WatchedEpisodesListResponse`):

```json
{
  "items": [
    {
      "episodeId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "tvShowId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "seasonId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
      "tvShowTitle": "Breaking Bad",
      "seasonNumber": 1,
      "episodeNumber": 1,
      "episodeTitle": "Pilot",
      "watchedAt": "2026-09-11T14:30:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

**Status codes:** `200`, `400`, `401`

---

### Recent history

#### `GET /api/watch-history/recent`

Merges watched movies and episodes, ordered by `watchedAt DESC`.

**Query parameters:** `page`, `pageSize`

**Response `200 OK`** (`RecentWatchHistoryResponse`):

```json
{
  "items": [
    {
      "type": "movie",
      "movieId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "episodeId": null,
      "tvShowId": null,
      "title": "Interstellar",
      "tvShowTitle": null,
      "seasonNumber": null,
      "episodeNumber": null,
      "episodeTitle": null,
      "watchedAt": "2026-09-11T14:30:00Z"
    },
    {
      "type": "episode",
      "movieId": null,
      "episodeId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "tvShowId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "title": null,
      "tvShowTitle": "Breaking Bad",
      "seasonNumber": 1,
      "episodeNumber": 3,
      "episodeTitle": "And the Bag's in the River",
      "watchedAt": "2026-09-11T13:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 2,
  "totalPages": 1,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

`type` values: `"movie"` or `"episode"`.

**Status codes:** `200`, `400`, `401`

---

### TV show progress

#### `GET /api/watch-history/tvshows/{tvShowId}`

**Response `200 OK`** (`TvShowWatchProgressResponse`):

```json
{
  "tvShowId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "totalEpisodes": 62,
  "watchedEpisodes": 15,
  "progressPercentage": 24.19,
  "nextEpisode": {
    "episodeId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "seasonNumber": 2,
    "episodeNumber": 3,
    "title": "Bit by a Dead Bee"
  }
}
```

`nextEpisode` is `null` when all episodes are watched.

**Status codes:** `200`, `401`, `404`

---

#### `GET /api/watch-history/tvshows/{tvShowId}/seasons/{seasonNumber}`

**Route parameter:** `seasonNumber` must be ≥ 1.

**Response `200 OK`** (`SeasonWatchProgressResponse`):

```json
{
  "tvShowId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "seasonNumber": 1,
  "totalEpisodes": 7,
  "watchedEpisodes": 3,
  "progressPercentage": 42.86,
  "nextEpisode": {
    "episodeId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "episodeNumber": 4,
    "title": "Cancer Man"
  }
}
```

**Status codes:** `200`, `400`, `401`, `404`

---

### Continue Watching limitations

Because there is no playback-position tracking:

- Continue Watching (in Home API) only shows TV shows with partially watched episodes.
- Movies never appear in Continue Watching.
- There is no "resume at 45:00" functionality.

---

## 16. Pagination

All paginated endpoints share this response envelope. Property names vary by endpoint (`items`, `movies`/`tvShows`, etc.) but pagination metadata is consistent.

### Shared pagination fields

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 150,
  "totalPages": 8,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

| Field | Description |
|-------|-------------|
| `page` | Current page number. **Starts at 1.** |
| `pageSize` | Items per page. Default `20`, max `100`. |
| `totalCount` | Total matching items across all pages. |
| `totalPages` | `ceil(totalCount / pageSize)`. `0` when `totalCount` is `0`. |
| `hasNextPage` | `true` when `page < totalPages`. |
| `hasPreviousPage` | `true` when `page > 1`. |

### Validation errors

| Condition | Message |
|-----------|---------|
| `page < 1` | `"Page must be at least 1."` |
| `pageSize < 1` | `"Page size must be at least 1."` |
| `pageSize > 100` | `"Page size must not exceed 100."` |

---

## 17. Error Handling

### Error response format

All controller-handled errors return `ProblemDetails`:

```json
{
  "status": 400,
  "title": "Invalid search request.",
  "detail": "Search query must be at least 2 characters."
}
```

No `type` URI is set. No custom error code envelope exists.

### Examples by status code

**400 Bad Request:**

```json
{
  "status": 400,
  "title": "Invalid registration request.",
  "detail": "Password must be at least 8 characters."
}
```

**401 Unauthorized:**

```json
{
  "status": 401,
  "title": "Authentication failed.",
  "detail": "Invalid email or password."
}
```

**404 Not Found:**

```json
{
  "status": 404,
  "title": "Movie not found.",
  "detail": "Movie with id '3fa85f64-5717-4562-b3fc-2c963f66afa6' was not found."
}
```

**409 Conflict:**

```json
{
  "status": 409,
  "title": "Registration conflict.",
  "detail": "A user with this email address already exists."
}
```

**500 Internal Server Error:**

No standardized application-level `500` response exists. Unhandled exceptions fall through to the ASP.NET Core default error handling, which may produce a generic `ProblemDetails` or empty body depending on environment configuration.

---

## 18. Content Types

### Query parameter `type` (input)

Accepted values (case-insensitive): `movie`, `tv`, `all`.

Used in: `/api/search`, `/api/discovery/*`, `/api/home`, `/api/recommendations`.

### Response content type strings (output)

| Field | Values | Used in |
|-------|--------|---------|
| `type` | `"movie"`, `"tv"` | Search items, recommendation items, recent watch history |
| `contentType` | `"movie"`, `"tv"` | Home items |

### Home section types (output)

PascalCase strings: `RecommendedForYou`, `BecauseYouWatched`, `BasedOnFavorites`, `ContinueWatching`, `Trending`, `Popular`, `NewReleases`, `TopRated`, `Genre`.

### Recommendation home section keys (output)

Kebab-case strings: `recommended-for-you`, `because-you-watched`, `similar-to-favorites`, `popular`, `trending`, `top-rated`.

---

## 19. Mobile Integration Rules

1. **Store JWT securely** — Use platform secure storage (e.g. `expo-secure-store`). Never store tokens in plain `AsyncStorage`.
2. **Send `Authorization: Bearer <token>`** on every authenticated request.
3. **Centralize API calls** — Use a single HTTP client (e.g. Axios/fetch wrapper) that attaches the token and base URL.
4. **Centralize HTTP error handling** — Parse `ProblemDetails` responses; map `status`/`title`/`detail` to user-facing messages.
5. **Do not hardcode API URLs** — Use environment configuration (`EXPO_PUBLIC_API_URL` or similar).
6. **Treat `401` as authentication expiration/failure** — Clear stored token and redirect to login.
7. **Do not send `userId`** — The backend derives the user from the JWT for all authenticated operations.
8. **Respect pagination** — Use `hasNextPage`/`hasPreviousPage` and standard `page`/`pageSize` parameters.
9. **Use cancellation/abort** — Pass `AbortSignal` to fetch calls; cancel in-flight requests on screen unmount or query change.
10. **Handle idempotent mutations** — Favorites, watchlist items, and watch history return `200` (already exists) or `201` (newly created). Handle both as success.
11. **Replace token on email/password change** — `ChangeEmail` and `ChangePassword` return a new `accessToken`; update stored token immediately.
12. **No server-side logout** — Discard the token locally to log out.

---

## 20. Complete Endpoint Index

| Method | Endpoint | Auth | Purpose |
|--------|----------|------|---------|
| POST | `/api/auth/register` | No | Register new user |
| POST | `/api/auth/login` | No | Login |
| GET | `/api/auth/me` | JWT | Get current user |
| GET | `/api/users/me` | JWT | Get current profile |
| PUT | `/api/users/me/profile` | JWT | Update display name |
| PUT | `/api/users/me/email` | JWT | Change email |
| PUT | `/api/users/me/password` | JWT | Change password |
| GET | `/api/users/me/statistics` | JWT | Get user statistics |
| DELETE | `/api/users/me` | JWT | Delete account |
| GET | `/api/movies/search` | No | Search movies (provider) |
| GET | `/api/movies/{id}` | No | Movie details |
| GET | `/api/tvshows/search` | No | Search TV shows (provider) |
| GET | `/api/tvshows/{id}` | No | TV show details |
| GET | `/api/tvshows/{id}/seasons/{seasonNumber}` | No | Season details |
| GET | `/api/tvshows/{id}/seasons/{seasonNumber}/episodes/{episodeNumber}` | No | Episode details |
| GET | `/api/search` | No | Unified catalog search |
| GET | `/api/search/autocomplete` | No | Search autocomplete |
| GET | `/api/search/history` | JWT | Get search history |
| DELETE | `/api/search/history` | JWT | Clear search history |
| DELETE | `/api/search/history/{id}` | JWT | Delete history item |
| GET | `/api/discovery/popular` | No | Popular content |
| GET | `/api/discovery/trending` | No | Trending content |
| GET | `/api/home` | JWT | Home screen sections |
| GET | `/api/recommendations/movies/{movieId}/similar` | No | Similar movies |
| GET | `/api/recommendations/tvshows/{tvShowId}/similar` | No | Similar TV shows |
| GET | `/api/recommendations` | JWT | Personalized recommendations |
| GET | `/api/recommendations/home` | JWT | Recommendation sections |
| POST | `/api/favorites/movies/{movieId}` | JWT | Add movie favorite |
| DELETE | `/api/favorites/movies/{movieId}` | JWT | Remove movie favorite |
| POST | `/api/favorites/tvshows/{tvShowId}` | JWT | Add TV favorite |
| DELETE | `/api/favorites/tvshows/{tvShowId}` | JWT | Remove TV favorite |
| GET | `/api/favorites` | JWT | List favorites |
| POST | `/api/watchlists` | JWT | Create watchlist |
| GET | `/api/watchlists` | JWT | List watchlists |
| GET | `/api/watchlists/{watchlistId}` | JWT | Get watchlist detail |
| DELETE | `/api/watchlists/{watchlistId}` | JWT | Delete watchlist |
| POST | `/api/watchlists/{watchlistId}/movies/{movieId}` | JWT | Add movie to watchlist |
| DELETE | `/api/watchlists/{watchlistId}/movies/{movieId}` | JWT | Remove movie from watchlist |
| POST | `/api/watchlists/{watchlistId}/tvshows/{tvShowId}` | JWT | Add TV show to watchlist |
| DELETE | `/api/watchlists/{watchlistId}/tvshows/{tvShowId}` | JWT | Remove TV show from watchlist |
| GET | `/api/watchlists/{watchlistId}/items` | JWT | List watchlist items |
| POST | `/api/ratings/movies/{movieId}` | JWT | Rate movie |
| POST | `/api/ratings/tvshows/{tvShowId}` | JWT | Rate TV show |
| DELETE | `/api/ratings/movies/{movieId}` | JWT | Delete movie rating |
| DELETE | `/api/ratings/tvshows/{tvShowId}` | JWT | Delete TV rating |
| GET | `/api/ratings/movies/{movieId}/me` | JWT | Get own movie rating |
| GET | `/api/ratings/tvshows/{tvShowId}/me` | JWT | Get own TV rating |
| GET | `/api/ratings/movies/{movieId}` | No | Movie rating aggregate |
| GET | `/api/ratings/tvshows/{tvShowId}` | No | TV rating aggregate |
| POST | `/api/reviews/movies/{movieId}` | JWT | Create movie review |
| POST | `/api/reviews/tvshows/{tvShowId}` | JWT | Create TV review |
| PUT | `/api/reviews/movies/{movieId}` | JWT | Update movie review |
| PUT | `/api/reviews/tvshows/{tvShowId}` | JWT | Update TV review |
| DELETE | `/api/reviews/movies/{movieId}` | JWT | Delete movie review |
| DELETE | `/api/reviews/tvshows/{tvShowId}` | JWT | Delete TV review |
| GET | `/api/reviews/movies/{movieId}/me` | JWT | Get own movie review |
| GET | `/api/reviews/tvshows/{tvShowId}/me` | JWT | Get own TV review |
| GET | `/api/reviews/movies/{movieId}` | No | Public movie reviews |
| GET | `/api/reviews/tvshows/{tvShowId}` | No | Public TV reviews |
| POST | `/api/watch-history/movies/{movieId}` | JWT | Mark movie watched |
| DELETE | `/api/watch-history/movies/{movieId}` | JWT | Unmark movie watched |
| GET | `/api/watch-history/movies/{movieId}/me` | JWT | Movie watch status |
| GET | `/api/watch-history/movies` | JWT | List watched movies |
| POST | `/api/watch-history/episodes/{episodeId}` | JWT | Mark episode watched |
| DELETE | `/api/watch-history/episodes/{episodeId}` | JWT | Unmark episode watched |
| GET | `/api/watch-history/episodes/{episodeId}/me` | JWT | Episode watch status |
| GET | `/api/watch-history/episodes` | JWT | List watched episodes |
| GET | `/api/watch-history/recent` | JWT | Recent watch history |
| GET | `/api/watch-history/tvshows/{tvShowId}` | JWT | TV show watch progress |
| GET | `/api/watch-history/tvshows/{tvShowId}/seasons/{seasonNumber}` | JWT | Season watch progress |
| GET | `/health` | No | Basic health check |
| GET | `/health/ready` | No | Readiness check (PostgreSQL + Redis) |

**Total: 72 endpoints** (70 mobile-relevant + 2 health)
