# MovieApp Search Load Tests

These tests are intentionally excluded from normal CI unit-test runs.

Run manually:

```bash
dotnet test tests/MovieApp.LoadTests/MovieApp.LoadTests.csproj --filter "Category=Load"
```

Scenarios exercise `SearchService` concurrency with the production lock/freshness flow using:

- local single-flight fallback (Redis unavailable path)
- stale catalog refresh coalescing
- slow provider behavior

For two-instance Redis distributed lock validation, use staging with Redis enabled and run the same query against two API containers while observing TMDB/provider call counts via application metrics/logs.
