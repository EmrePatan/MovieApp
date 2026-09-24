# Expected HTTP status semantics (#60)

Used by `lib/http.js` via the `expectation` option. **401/403** are always failures. **5xx** and **timeouts** are always failures. **404** is only success when the expectation allows state absence.

| Endpoint pattern | Expectation | Acceptable statuses | Notes |
|------------------|-------------|---------------------|-------|
| `GET /api/ratings/movies/{id}/me` | `USER_STATE_ABSENT_404` | 200, 404 | No user rating |
| `GET /api/ratings/tvshows/{id}/me` | `USER_STATE_ABSENT_404` | 200, 404 | No user rating |
| `GET /api/favorites/movies/{id}/status` | `API_SUCCESS` | 2xx | Body carries boolean |
| `GET /api/favorites/tvshows/{id}/status` | `API_SUCCESS` | 2xx | Body carries boolean |
| `GET /api/watchlists/membership` | `API_SUCCESS` | 2xx | Membership lists |
| `GET /api/watch-history/movies/{id}/me` | `API_SUCCESS` | 2xx | Unwatched → 200 + `watched: false` |
| `GET /api/movies/{id}/follow` | `API_SUCCESS` | 2xx | Follow state in body |
| `GET /api/tvshows/{id}/follow` | `API_SUCCESS` | 2xx | Follow state in body |
| `GET /api/movies/{id}`, TV detail, credits, reviews list | `API_SUCCESS` | 2xx | 404 ⇒ bad catalog ID in pool |
| Home, library, insights, personalized | `API_SUCCESS` | 2xx | 401 ⇒ token issue |
| Discovery (public reads) | `API_SUCCESS` | 2xx | 503 ⇒ provider unavailable (failure) |
| `GET /api/search/*`, movie/tv search | `RATE_LIMIT_AWARE` | 2xx, 429 | 429 tracked separately |
| `GET /health/*` | `CONTROL_PLANE` | 2xx | Excluded from app RPS (`workload_scope=control`) |

Custom metrics: `semantic_success`, `unexpected_status`, `rate_limited`, `rate_limited_count`.

k6 `http_req_failed` uses `responseCallback` so legitimate 404/429 (per class) do not inflate failure rate.
