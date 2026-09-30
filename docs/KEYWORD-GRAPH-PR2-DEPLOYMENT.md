# Keyword graph PR2 deployment runbook

PR2 introduces provider-aware TMDB keyword sync behind `KeywordGraph:ProviderAwareSyncEnabled` (default **false**). Deploying PR2 alone does **not** switch production to the provenance graph.

## Preconditions

- PR1 migration `20260930100106_ExpandKeywordGraphPr1` applied in all environments.
- All application instances run the PR2+ binary (no old writers creating keywords without dual-write metadata).

## Safe production sequence

1. Deploy PR2 with `KeywordGraph:ProviderAwareSyncEnabled` **false** (default in `appsettings.json`; do not enable in Production config yet).
2. Confirm no instances are running the pre-PR2 binary.
3. Run keyword graph reconciliation explicitly (not on startup):
   - Resolve `IKeywordGraphReconciliationService` from DI in a controlled admin/ops context and call `ReconcileAsync()`.
   - Do not register reconciliation as a recurring Hangfire job in this phase.
4. Verify `KeywordGraphReconciliationResult`:
   - `MissingCanonicalNameCount == 0`
   - `MissingNormalizedNameCount == 0`
   - `MissingTmdbExternalRefCount == 0`
   - `MissingMovieTmdbSourceCount == 0`
   - `MissingTvTmdbSourceCount == 0`
   - `ConflictingExternalReferenceCount == 0`
   - `IsReadyForProviderAwareSync == true`
5. Run `keyword-graph-verify-readiness` again immediately before the config change.
6. Only then set `KeywordGraph:ProviderAwareSyncEnabled` to **true** in Production configuration and roll instances.
7. Run a controlled TMDB keyword sync smoke test on a few titles.
8. Re-run `keyword-graph-verify-readiness` and confirm invariants.

## Transactions (provider-aware sync)

`ExecuteInRetriableTransactionAsync` opens one database transaction; `SaveChanges` flushes to that transaction only. Commit happens once at the end; any exception rolls back source diff, materialized joins, and marker updates together.

## Production invocation (Render / ops)

Use the existing **ContentSearchTitleOps** CLI (same PostgreSQL env vars as other maintenance tools). Run as a **one-off job/shell**, not via the API:

```bash
dotnet run --project tools/ContentSearchTitleOps -- keyword-graph-reconcile
dotnet run --project tools/ContentSearchTitleOps -- keyword-graph-verify-readiness
```

- Exit code `0` = `IsReadyForProviderAwareSync=true`; `2` = not ready (see stdout counters/conflicts).
- **Immediately before** enabling `ProviderAwareSyncEnabled`, run `keyword-graph-verify-readiness` again (switch-time check; do not rely on an hours-old reconcile snapshot).
- No Hangfire schedule, no startup hook, no public HTTP endpoint.

## Operational notes

- While the flag is **false**, TMDB sync continues to replace `movie_keywords` / `tv_show_keywords` directly (legacy). **TMDB** rows in `movie_keyword_sources` / `tv_show_keyword_sources` are mirrored on each legacy sync (shadow provenance) so sources do not drift before the flag is enabled. Non-TMDB sources (e.g. test fixtures) are not removed by TMDB sync. Source tables are still **not** authoritative for reads/recommendations until the flag is **true**.
- While the flag is **false**, new TMDB keywords still receive `CanonicalName`, `NormalizedName`, and TMDB `keyword_external_references` dual-write.
- When the flag is **true**, TMDB refresh only reconciles `Provider = Tmdb` source rows; other providers are untouched. Materialized joins are removed only when **no** source remains for that title and keyword.
- `NormalizedName` is computed with `KeywordCanonicalNormalization` (culture-invariant). Turkish display/search normalization is a separate future layer.
- MDBList ingestion is **not** part of PR2; `MdbListKeywordsSyncedAtUtc` is unchanged by TMDB sync.

## Rollback

- Set `KeywordGraph:ProviderAwareSyncEnabled` back to **false** to restore legacy TMDB join replacement without dropping PR1 schema.
