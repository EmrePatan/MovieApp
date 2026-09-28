import assert from "node:assert/strict";
import { test } from "node:test";

// Mirrors worker redirect rules for focused validation.
const CATALOG_PATH_PATTERN = /^\/(movie|tv)\/([^/?#]+)/;

function shouldRedirectOpenHostToCanonical(hostname, pathname) {
  return hostname === "open.moviecaveapp.com" && CATALOG_PATH_PATTERN.test(pathname);
}

function buildCanonicalRedirect(pathname, search = "") {
  return `https://moviecaveapp.com${pathname}${search}`;
}

test("open subdomain catalog path redirects to canonical without loop", () => {
  const pathname = "/movie/3fa85f64-5717-4562-b3fc-2c963f66afa6";
  assert.equal(shouldRedirectOpenHostToCanonical("open.moviecaveapp.com", pathname), true);
  const target = buildCanonicalRedirect(pathname);
  assert.equal(target.startsWith("https://moviecaveapp.com/movie/"), true);
  assert.equal(shouldRedirectOpenHostToCanonical("moviecaveapp.com", pathname), false);
});

test("canonical host does not redirect to open", () => {
  const pathname = "/movie/3fa85f64-5717-4562-b3fc-2c963f66afa6";
  assert.equal(shouldRedirectOpenHostToCanonical("moviecaveapp.com", pathname), false);
});

const PUBLIC_WEB_PATHS = new Set(["/", "/movies", "/tv"]);
const WELL_KNOWN_PATHS = new Set([
  "/.well-known/apple-app-site-association",
  "/.well-known/assetlinks.json",
]);

function shouldProxyToApi(pathname) {
  return (
    PUBLIC_WEB_PATHS.has(pathname) ||
    CATALOG_PATH_PATTERN.test(pathname) ||
    WELL_KNOWN_PATHS.has(pathname)
  );
}

test("public landing paths proxy to API when configured", () => {
  assert.equal(shouldProxyToApi("/"), true);
  assert.equal(shouldProxyToApi("/movies"), true);
  assert.equal(shouldProxyToApi("/tv"), true);
  assert.equal(shouldProxyToApi("/delete-account"), false);
});

/** Mirrors wrangler.toml assets.run_worker_first = ["/"] */
const RUN_WORKER_FIRST_PATHS = ["/"];

function invokesWorkerBeforeAssets(pathname) {
  return RUN_WORKER_FIRST_PATHS.includes(pathname);
}

function resolveCanonicalHostRouting(pathname, apiOrigin) {
  if (invokesWorkerBeforeAssets(pathname) && shouldProxyToApi(pathname) && apiOrigin) {
    return { action: "proxy", target: `${apiOrigin.replace(/\/$/, "")}${pathname}` };
  }

  if (shouldProxyToApi(pathname) && apiOrigin) {
    return { action: "proxy", target: `${apiOrigin.replace(/\/$/, "")}${pathname}` };
  }

  return { action: "assets" };
}

test("root / proxies to API even when dist/index.html exists (run_worker_first)", () => {
  const apiOrigin = "https://movieapp-fpkg.onrender.com";
  assert.equal(invokesWorkerBeforeAssets("/"), true);
  const routing = resolveCanonicalHostRouting("/", apiOrigin);
  assert.equal(routing.action, "proxy");
  assert.equal(routing.target, "https://movieapp-fpkg.onrender.com/");
});

test("static legal pages still use ASSETS (no API proxy)", () => {
  const apiOrigin = "https://movieapp-fpkg.onrender.com";
  for (const pathname of ["/privacy", "/terms", "/delete-account", "/auth/verify-email"]) {
    assert.equal(shouldProxyToApi(pathname), false);
    assert.equal(invokesWorkerBeforeAssets(pathname), false);
    assert.equal(resolveCanonicalHostRouting(pathname, apiOrigin).action, "assets");
  }
});
