# Issue #60 — capacity outcome (release-facing)

**Last updated:** 2026-09-25  
**Status:** **Release-readiness portion — PASS / complete.** Higher-scale capacity work continues as **post-release** engineering (see below).

This document records the **controlled 100-VU production diagnostic** and how it relates to earlier anomalous results. It does **not** change production behavior, thresholds, or infrastructure.

---

## What #60 means now (split)

| Track | Scope | Status |
|-------|--------|--------|
| **A — Release readiness capacity baseline** | Tooling, runbook, identity model, thresholds, and a **sustained 100-VU** production diagnostic under `user-concurrency` / `capacity` / `hot` | **Complete — PASS** |
| **B — Post-release scale & resilience** | Stages **150 / 250 / 500 / 1000** VU, cache stampede hardening, sizing/cost curve, deeper transient-degradation investigation if events recur | **Open — not a Friends & Family launch blocker** |

**#60 no longer blocks** Friends & Family Beta **solely** because 150/250/500/1000-VU stages have not been executed.

---

## Controlled 100-VU diagnostic (2026-09-25) — PASS

### Configuration

| Field | Value |
|-------|--------|
| Date | 2026-09-25 |
| ExecutionMode | Local (`k6 run`, `tokens.json`) |
| Scenario | `user-concurrency` |
| Preset | `capacity` (3m ramp → **12m hold @ 100 VU** → 2m ramp-down) |
| StageTarget | 100 |
| ContentDataset | `hot` |
| Target | Production API |
| Identities | 100 dedicated load-test users |
| Instrumentation | Deployed from commit `c74face` (`DiscoveryPerf` + existing Home/Rec/Insights perf logs) |

### k6 summary (final)

| Metric | Result |
|--------|--------|
| HTTP requests | 15,440 |
| Completed iterations | 6,018 |
| Interrupted iterations | 0 |
| Checks | 100% |
| `http_req_failed` | 0.00% |
| `semantic_success` | 100% |
| `unexpected_status` | 0.00% |
| Overall avg | 102.34 ms |
| Overall p95 | 204.14 ms |
| Overall p99 | 437.92 ms |
| Max | 4.36 s |
| Application RPS | ~14.82 req/s |
| Configured thresholds | **All passed** |

### Endpoint group p95 (selected)

| Group | p95 |
|-------|-----|
| home | 323.55 ms |
| personalized | 291.92 ms |
| recommendations-home | 196.41 ms |
| insights | 492.35 ms |
| library | 357.92 ms |
| search | 150.56 ms |
| discover | 159.23 ms |
| tv-detail | 151.07 ms |
| detail-status | 174.97 ms |

### Render (0.1 vCPU / 0.25 GB)

- CPU increased under load but **retained substantial headroom** (not comparable to pegged ~100% in run 8625084).
- Memory **well below** the instance limit.
- No sustained saturation or crash observed during this window.

### Report artifact

Archive path (operator machine; `reports/` is gitignored):

`tests/load/reports/user-concurrency-capacity-100-2026-09-25T00-47-53Z.json`

---

## Capacity interpretation (what this proves)

- **100 sustained virtual users** in this harness (looping journeys with think time) **did not exceed** observed production capacity on the current Render tier for the **17-minute** capacity window.
- This is **not** a claim that production supports “only 100 registered users.” It models up to **100 simultaneously active, looping VUs** — substantially **more aggressive** than expected initial Friends & Family usage patterns.
- **Higher concurrency (150+)** remains **unproven** until separately executed and analyzed under the same runbook gates.

---

## Grafana Cloud run 8625084 — anomalous; not the 100-VU baseline

Run **8625084** (Grafana Cloud, same scenario family) showed **severe transient degradation** (e.g. ~8% HTTP failures, overall p95 ~26 s, p99 ~29.7 s, widespread status=0 ~30 s timeouts, Render CPU saturation). That outcome was **not reproduced** by the controlled 2026-09-25 local diagnostic.

**Classification:** treat 8625084 as an **anomalous / transient production degradation event**. The exact initiating cause remains **unproven**. Do **not** document 8625084 as the normal or expected result of a healthy 100-VU capacity run.

---

## Cache / discovery timeline (operator notes)

### Pre-test degradation (before ~03:48 run start, operator-local timestamps)

Examples from `DiscoveryPerf` immediately before the diagnostic:

- **03:45:39** — Trending `TotalLoadMs`≈1693; NewReleases ≈1803; TopRated ≈2105.
- **03:47:43** — NewReleases `CacheLookupMs`≈2501, `CanonicalLoadMs`≈3701, `CacheWriteMs`≈1107, `TotalLoadMs`≈7407; Trending `CacheLookupMs`≈3202, `CanonicalLoadMs`≈4400, `CacheWriteMs`≈600, `TotalLoadMs`≈8203.

Redis lookup, canonical DB load, and cache write were **simultaneously slow**, indicating **transient infrastructure or application stress** before the test. Available evidence does **not** establish root cause.

### During the 100-VU hold — healthy discovery reloads

Later `DiscoveryPerf Cache=LOAD_COMPLETED` samples during the run were **low milliseconds to low hundreds of ms** (e.g. Trending 8–97 ms, Popular ~10–29 ms, TopRated ~14–37 ms, NewReleases ~13 ms) across expected **~5 / ~10 minute** discovery TTL windows.

**Interpretation (careful):**

- The simple hypothesis *“routine 5-minute cache expiry alone causes service collapse”* was **not reproduced** on this run.
- Multiple discovery reload / expiry windows occurred during the successful 17-minute hold **without** CPU saturation, request failures, or timeout tails comparable to 8625084.

---

## Technical debt (code — not proven cause of 8625084)

Still true in codebase; track as **post-release resilience**, not as attributed root cause for 8625084:

- Discovery, home, recommendation-home, and insights read paths lack **general** single-flight / stampede protection on cache miss.
- **No TTL jitter** on absolute Redis TTLs.
- Insights V3 cache miss can fan out across **multiple scoped DbContexts** (high per-miss connection demand).

---

## Post-release follow-up (track B)

Execute when product traffic or business goals require higher assurance:

1. **Staged higher-VU production runs** — 150 → 250 → … per `RUNBOOK.md` gates (local generator if Cloud VU caps apply).
2. **Cache stampede resilience** — single-flight / jitter / SWR (design + implement; observability-only work landed in `c74face` / `b4f642d`).
3. **Infrastructure sizing & cost** — Render CPU/memory tier vs RPS and tail latency.
4. **Transient degradation playbook** — if events like 8625084 recur, capture same-clock k6 + Render + `DiscoveryPerf` + pool/Redis metrics per `RUNBOOK.md` diagnostic section.

---

## Related docs

- [`RUNBOOK.md`](../RUNBOOK.md) — operator execution, abort conditions, monitoring checklists
- [`README.md`](../README.md) — scenario model and traffic weights
