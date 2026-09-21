# Movie Cave public website — Cloudflare Workers (Git) deployment

Official domain: **https://moviecaveapp.com**

This folder is a **static** site with no backend dependency. It is deployed with **Cloudflare Workers Static Assets** via the current **Workers Git** integration (not legacy Pages root/output-directory fields).

## Repository layout

| Path | Purpose |
|------|---------|
| `index.html` | `/` |
| `privacy/index.html` | `/privacy/` |
| `terms/index.html` | `/terms/` |
| `delete-account/index.html` | `/delete-account/` |
| `wrangler.toml` | Workers static-assets config (`assets.directory = "./dist"`) |
| `scripts/build.mjs` | Validates source files and stages public assets into `dist/` |
| `package.json` | `npm run build` + Wrangler devDependency |

There is **no** Worker `main` script. Wrangler serves files from this directory only.

## Cloudflare dashboard — exact values

Use these on the **Workers Git** connect screen (Project name / Build command / Deploy command / Advanced settings).

| # | Field | Value |
|---|--------|--------|
| 1 | **Project name** | `moviecaveapp` |
| 2 | **Build command** | `npm ci && npm run build` |
| 3 | **Deploy command** | `npx wrangler deploy` |
| 4 | **Builds for non-production branches** | **Enabled** (recommended). Non-`master` commits run `npx wrangler versions upload` by default and produce preview URLs without promoting production. |
| 5 | **Cloudflare Access** | **Disabled** (public marketing + legal pages must be reachable without login). |
| 6 | **Advanced settings** | See table below. |
| 7 | **Root directory** (under Advanced) | `website` |
| 8 | **workers.dev URL** | After first production deploy: `https://moviecaveapp.<your-account-subdomain>.workers.dev` (subdomain is account-specific; shown on the Worker overview page). |
| 9 | **Custom domain** | See [Attach moviecaveapp.com](#attach-moviecaveappcom-after-first-deploy) below. |

### Advanced settings (change only these)

| Advanced field | Value | Notes |
|----------------|--------|--------|
| **Root directory** | `website` | Required. Build and deploy commands run from this folder. |
| **Production branch** | `master` | Match the repo default branch. |
| **Non-production branch deploy command** | *(leave default)* `npx wrangler versions upload` | Only used when non-production branch builds are enabled. |
| **API token** | *(leave default)* Automatically generated | Do not commit tokens to git. |
| **Build variables and secrets** | *(none)* | Not required for this static site. |

All other advanced fields: **leave at defaults**.

**Critical:** Dashboard **Project name** must match `name = "moviecaveapp"` in `website/wrangler.toml`. A mismatch fails the build.

## Connect GitHub (first time)

1. Cloudflare dashboard → **Workers & Pages** → **Create** → connect **Git** → select **EmrePatan/MovieApp**.
2. Enter the values from the table above.
3. Under **Advanced settings**, set **Root directory** to `website`.
4. Save and deploy (or push a commit to `master`).

## Routing

Directory-based static routes (default `html_handling = "auto-trailing-slash"`):

- `/` → `index.html`
- `/privacy/` → `privacy/index.html`
- `/terms/` → `terms/index.html`
- `/delete-account/` → `delete-account/index.html`

No SPA fallback is configured.

## Attach moviecaveapp.com after first deploy

1. Confirm production deploy succeeded on `master` and the `*.workers.dev` URL serves all four routes.
2. Open the **moviecaveapp** Worker → **Settings** → **Domains & Routes** (or **Triggers** → **Custom Domains**).
3. **Add custom domain** → `moviecaveapp.com` (and optionally `www.moviecaveapp.com`).
4. If `moviecaveapp.com` is already on Cloudflare DNS, Cloudflare usually creates the required records automatically. Otherwise add the CNAME/AAAA records shown in the UI.
5. Wait for certificate provisioning (typically minutes). Verify:
   - https://moviecaveapp.com/
   - https://moviecaveapp.com/privacy/
   - https://moviecaveapp.com/terms/
   - https://moviecaveapp.com/delete-account/
6. Optional: add a **Redirect Rule** for `www` → apex if you attach both hostnames.

## Local validation

From the repository root:

```bash
cd website
npm ci
npm run build
```

Optional local preview (Wrangler static assets server):

```bash
npm run preview
```

Then open:

- http://localhost:8787/
- http://localhost:8787/privacy/
- http://localhost:8787/terms/
- http://localhost:8787/delete-account/

Deploy to Cloudflare (requires local `wrangler login`; not needed for CI):

```bash
npm run deploy
```

## Credentials

Do **not** store Cloudflare API tokens, account IDs, or other secrets in this repository. Workers Builds uses a dashboard-managed API token.
