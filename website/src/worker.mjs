/**
 * Proxies public catalog share pages and app-link association files to the MovieApp API.
 * Static marketing/legal pages continue to be served from the ASSETS binding.
 *
 * open.moviecaveapp.com catalog paths redirect to canonical moviecaveapp.com (no duplicate HTML).
 */
const CATALOG_PATH_PATTERN = /^\/(movie|tv)\/([^/?#]+)/;
const WELL_KNOWN_PATHS = new Set([
  "/.well-known/apple-app-site-association",
  "/.well-known/assetlinks.json",
]);

function resolveCanonicalOrigin(env) {
  const configured = env.CATALOG_SHARE_CANONICAL_ORIGIN?.trim();
  if (configured) {
    return configured.replace(/\/$/, "");
  }

  return "https://moviecaveapp.com";
}

function resolveAppOpenHost(env) {
  const configured = env.CATALOG_SHARE_APP_OPEN_HOST?.trim();
  if (configured) {
    return configured.toLowerCase();
  }

  return "open.moviecaveapp.com";
}

function isCatalogPath(pathname) {
  return CATALOG_PATH_PATTERN.test(pathname);
}

const PUBLIC_WEB_PATHS = new Set(["/", "/movies", "/tv"]);

function shouldProxyToApi(pathname) {
  return (
    PUBLIC_WEB_PATHS.has(pathname) ||
    isCatalogPath(pathname) ||
    WELL_KNOWN_PATHS.has(pathname)
  );
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const pathname = url.pathname;
    const host = url.hostname.toLowerCase();
    const appOpenHost = resolveAppOpenHost(env);
    const canonicalOrigin = resolveCanonicalOrigin(env);

    if (host === appOpenHost && isCatalogPath(pathname)) {
      const canonicalUrl = `${canonicalOrigin}${pathname}${url.search}`;
      return Response.redirect(canonicalUrl, 302);
    }

    const apiOrigin = env.CATALOG_SHARE_API_ORIGIN?.trim();
    if (shouldProxyToApi(pathname) && apiOrigin) {
      const target = new URL(`${pathname}${url.search}`, apiOrigin.replace(/\/$/, ""));
      const proxied = new Request(target.toString(), {
        method: request.method,
        headers: request.headers,
        redirect: "manual",
      });
      return fetch(proxied);
    }

    return env.ASSETS.fetch(request);
  },
};
