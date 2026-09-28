import assert from "node:assert/strict";
import { test } from "node:test";
import {
  buildProxyHeaders,
  canProxyPublicMethod,
  isCatalogPath,
  isWatchlistPath,
  normalizePublicPath,
  shouldProxyToApi,
} from "../src/worker.mjs";

function shouldRedirectOpenHostToCanonical(hostname, pathname) {
  const normalized = normalizePublicPath(pathname);
  return hostname === "open.moviecaveapp.com" && (isCatalogPath(normalized) || isWatchlistPath(normalized));
}

function buildCanonicalRedirect(pathname, search = "") {
  return `https://moviecaveapp.com${normalizePublicPath(pathname)}${search}`;
}

test("open subdomain catalog path redirects to canonical without loop", () => {
  const pathname = "/movie/3fa85f64-5717-4562-b3fc-2c963f66afa6";
  assert.equal(shouldRedirectOpenHostToCanonical("open.moviecaveapp.com", pathname), true);
  const target = buildCanonicalRedirect(pathname);
  assert.equal(target.startsWith("https://moviecaveapp.com/movie/"), true);
  assert.equal(shouldRedirectOpenHostToCanonical("moviecaveapp.com", pathname), false);
});

test("open subdomain watchlist path redirects to canonical", () => {
  const pathname = "/watchlist/abc123token";
  assert.equal(shouldRedirectOpenHostToCanonical("open.moviecaveapp.com", pathname), true);
  assert.equal(buildCanonicalRedirect(pathname), "https://moviecaveapp.com/watchlist/abc123token");
});

test("canonical host does not redirect to open", () => {
  const pathname = "/movie/3fa85f64-5717-4562-b3fc-2c963f66afa6";
  assert.equal(shouldRedirectOpenHostToCanonical("moviecaveapp.com", pathname), false);
});

test("public landing paths proxy to API when configured", () => {
  assert.equal(shouldProxyToApi("/"), true);
  assert.equal(shouldProxyToApi("/movies"), true);
  assert.equal(shouldProxyToApi("/tv"), true);
  assert.equal(shouldProxyToApi(normalizePublicPath("/movies/")), true);
  assert.equal(shouldProxyToApi("/watchlist/abc123token"), true);
  assert.equal(shouldProxyToApi("/delete-account"), false);
});

test("catalog proxy ignores extra path segments and forwards only safe headers", () => {
  const id = "3fa85f64-5717-4562-b3fc-2c963f66afa6";
  assert.equal(shouldProxyToApi(`/movie/${id}`), true);
  assert.equal(shouldProxyToApi(normalizePublicPath(`/tv/${id}/`)), true);
  assert.equal(shouldProxyToApi(`/movie/${id}/extra`), false);
  assert.equal(shouldProxyToApi(`/watchlist/${id}/extra`), false);
  assert.equal(canProxyPublicMethod("GET"), true);
  assert.equal(canProxyPublicMethod("POST"), false);

  const headers = buildProxyHeaders(new Headers({
    "Accept-Language": "tr-TR,tr;q=0.9",
    Accept: "text/html",
    Cookie: "session=secret",
    Authorization: "Bearer secret",
  }));
  assert.equal(headers.get("Accept-Language"), "tr-TR,tr;q=0.9");
  assert.equal(headers.get("Accept"), "text/html");
  assert.equal(headers.get("Cookie"), null);
  assert.equal(headers.get("Authorization"), null);
});

/** Mirrors wrangler.toml assets.run_worker_first = ["/"] */
const RUN_WORKER_FIRST_PATHS = ["/"];

function invokesWorkerBeforeAssets(pathname) {
  return RUN_WORKER_FIRST_PATHS.includes(pathname);
}

function resolveCanonicalHostRouting(pathname, apiOrigin) {
  const normalized = normalizePublicPath(pathname);
  if (shouldProxyToApi(normalized) && apiOrigin) {
    return { action: "proxy", target: `${apiOrigin.replace(/\/$/, "")}${normalized}` };
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
