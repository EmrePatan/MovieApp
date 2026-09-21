# Movie Cave public website — Cloudflare Pages deployment

Official domain: **https://moviecaveapp.com**

This folder is a static site with no backend dependency.

## Build settings

| Setting | Value |
|--------|--------|
| Framework preset | None |
| Build command | `npm run build` |
| Build output directory | `/` (repository root of this `website/` folder) |
| Root directory | `website` (when connecting the MovieApp GitHub repo) |

`npm run build` only validates required files exist. There is no compile step.

## Deploy from GitHub (recommended)

1. In Cloudflare Dashboard → **Workers & Pages** → **Create application** → **Pages** → **Connect to Git**.
2. Select the `MovieApp` repository.
3. Set **Production branch** to `master`.
4. Configure:
   - **Root directory:** `website`
   - **Build command:** `npm run build`
   - **Build output directory:** `/`
5. Deploy.

## Custom domain

1. Pages project → **Custom domains** → add `moviecaveapp.com`.
2. Add the DNS records Cloudflare shows (usually a CNAME to the Pages hostname, or use Cloudflare as DNS registrar/nameserver).
3. Enable HTTPS (automatic on Cloudflare Pages once DNS is active).

## Routing

Routes are directory-based:

- `/` → `index.html`
- `/privacy/` → `privacy/index.html`
- `/terms/` → `terms/index.html`
- `/delete-account/` → `delete-account/index.html`

No SPA fallback is required.

## Redirects (optional)

If you want to force HTTPS or add `www` → apex redirects, configure **Bulk Redirects** or **Redirect Rules** in Cloudflare after the domain is attached.

## Local validation

```bash
cd website
npm run validate
```

Serve locally with any static file server, for example:

```bash
npx serve .
```

Then open:

- http://localhost:3000/
- http://localhost:3000/privacy/
- http://localhost:3000/terms/
- http://localhost:3000/delete-account/

## Credentials

Do **not** store Cloudflare API tokens in this repository.
