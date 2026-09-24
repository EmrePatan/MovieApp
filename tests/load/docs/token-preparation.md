# Load-test JWT preparation

## Token lifetime

Movie App issues JWT access tokens via `JwtOptions.AccessTokenMinutes` (default **60 minutes** in code). Production configuration may differ — confirm deployed settings before a multi-hour capacity campaign.

A full #60 campaign can exceed token lifetime:

| Phase | Typical duration |
|-------|------------------|
| 5 VU baseline | ~2 min smoke + observation |
| Each capacity stage | ~17 min (ramp + 12m hold + ramp-down) × 6 stages |
| Cooldowns | 10 min × 5 |
| **Total** | **~2+ hours** minimum |

**Pre-generated tokens must remain valid for the entire window**, including cooldowns and optional reruns, **or** operators must **re-mint tokens between stages** (out of band — not via k6 login traffic).

## Safe preparation workflow

1. Create **dedicated load-test users** (never real customers).
2. Mint JWTs with expiry covering the full planned window (e.g. configure extended `AccessTokenMinutes` on a **staging** issuer, or use an internal admin/script that calls the same `JwtTokenService` used in tests).
3. Store in gitignored `data/tokens.json` with optional `expiresAtUtc` per identity for operator tracking.
4. Before each stage, spot-check one token with `GET /api/home` (preflight, not measured).
5. If a token expires mid-run, **stop the stage** — 401s will appear in `unexpected_status`, not as benign state.

## Identity count

| VUs | Recommendation |
|-----|----------------|
| 50–250 | Minimum **50** identities |
| 500–1000 | **100+** identities preferred (50 supported but increases per-user cache/personalization contention) |

Round-robin: `identity = pool[(vu-1) % N]`.

Do **not** add login/password traffic to measured scenarios.

## Provisioning (production)

There is **no** bulk admin or seed utility in the MovieApp repo. The supported path on current code is:

1. `POST /api/auth/register` + `POST /api/auth/verify-email` (or login after verify) **outside k6**, throttled per `Authentication:RateLimit`.
2. Store `AuthResponse.AccessToken` and `ExpiresAt` in gitignored `data/tokens.json`.
3. Before each stage run `scripts/Test-LoadTokens.ps1`.

See [`OPERATOR-INPUTS.md`](OPERATOR-INPUTS.md) for full comparison of options and identity counts.
