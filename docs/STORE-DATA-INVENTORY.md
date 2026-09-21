# Store data inventory — Movie Cave (#51)

Use this document when completing **Google Play Data Safety** and **Apple App Privacy** questionnaires. Values reflect the current codebase as of September 21, 2026.

Legend:
- **Collected?** — transmitted to or stored by Movie Cave backend/services
- **Linked?** — can be associated with account identity
- **Shared** — sent to a third-party processor to operate the service (not sold)

| Data category | Collected? | Linked to identity? | Purpose | Stored? | Shared with processor? | Provider / notes | Deletion behavior |
|---------------|------------|---------------------|---------|---------|------------------------|----------------|-------------------|
| Email address | Yes | Yes | Account, auth, transactional email | Yes (DB) | Yes (email delivery) | Resend | Deleted on account deletion |
| Username / display name | Yes | Yes | Account/profile | Yes (DB) | No | — | Deleted on account deletion |
| Password | Yes (hash only) | Yes | Authentication | Yes (DB hash) | No | — | Deleted on account deletion |
| User IDs | Yes | Yes | Internal account key | Yes (DB) | No | — | Deleted on account deletion |
| Social auth identifiers | Yes | Yes | Sign-in linking | Yes (DB) | Yes (token validation) | Google, Apple | Deleted on account deletion |
| Ratings | Yes | Yes | User feature | Yes (DB) | No | — | Deleted on account deletion |
| Reviews / user-generated text | Yes | Yes | User feature | Yes (DB) | No | — | Deleted on account deletion |
| Favorites / watchlists | Yes | Yes | User feature | Yes (DB) | No | — | Deleted on account deletion |
| Watch history | Yes | Yes | User feature | Yes (DB) | No | — | Deleted on account deletion |
| Search history | Yes | Yes | User feature | Yes (DB) | No | — | Deleted on account deletion |
| Follow / notification preferences | Yes | Yes | TV follow alerts | Yes (DB) | No | — | Deleted on account deletion |
| In-app notifications | Yes | Yes | Release alerts inbox | Yes (DB) | No | — | Deleted on account deletion; read items may auto-delete after retention window |
| Push token | Yes | Yes | Push delivery | Yes (DB) | Yes | Expo push infrastructure | Deleted on account deletion / logout unregister |
| Device platform (ios/android) | Yes | Yes | Push routing | Yes (DB) | Yes | Expo | Deleted with push device row |
| Product metric event names | Yes | No* | Aggregated usage counters | Yes (aggregated DB) | No | — | Not user-linked in metrics table |
| IP address | Transient | Unclear** | Rate limiting / security | Not in user DB | No | Hosting provider may log | Not stored as user profile field |
| Server logs (user id, correlation id) | Yes | Possibly | Operations/security | Logs/backups | Yes (hosting) | Render / infra | May persist for operational retention |
| AI taste profile + prompt | Yes (when feature used) | Indirect*** | Recommendations | Redis session TTL | Yes | Google Gemini | Session expires by TTL; not explicitly purged on delete |
| Catalog metadata requests | Yes | No | App functionality | Cached/shared catalog | Yes | TMDB, TVDB | Shared catalog, not personal |
| Poster/image fetches | Yes (device) | No | Display artwork | Device cache | Yes | TMDB CDN | Local device cache only |

\* Product metrics requests are unauthenticated and store only metric name + aggregate count.

\** IP is used for rate limiting partitions; no dedicated per-user IP history table was found.

\*** Gemini requests contain taste-related title/genre data derived from the user's account activity but not the raw account ID/email in the current prompt builder.

## Authentication providers to disclose

- Email/password (with verification)
- Google Sign-In
- Apple Sign-In (iOS)

## Features requiring extra disclosure

- **Push notifications** (optional; device permission required)
- **AI recommendations** (optional; sends derived taste data + user message to Google Gemini)
- **Transactional email** (verification, password reset)

## Uncertain / operator-dependent items

- Exact log and backup retention duration on Render (not encoded in application source)
- Whether inbound `support@moviecaveapp.com` will be configured (not present in repo)
- Whether Google Play will accept in-app-only deletion without a web form (documented on `/delete-account`)
