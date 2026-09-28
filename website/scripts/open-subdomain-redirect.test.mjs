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
