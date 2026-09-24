import { check } from 'k6';
import { loadThresholds } from '../lib/thresholds.js';

export const options = {
  vus: 1,
  iterations: 1,
};

function isObserveOnlyThreshold(entry, expectedExpr) {
  return (
    Array.isArray(entry) &&
    entry.length === 1 &&
    entry[0].threshold === expectedExpr &&
    entry[0].abortOnFail === false
  );
}

function isGatingThreshold(entry, expectedExpr) {
  return Array.isArray(entry) && entry[0] === expectedExpr;
}

export default function thresholdsConfigSelfCheck() {
  const thresholds = loadThresholds();

  check(thresholds, {
    'tv show detail gated by name tag': (t) =>
      isGatingThreshold(t['http_req_duration{name:tv-detail}'], 'p(95)<3500'),
    'tv show detail not gated by combined group tag': (t) =>
      t['http_req_duration{group:tv-detail}'] === undefined,
    'tv season-1 threshold is observability-only': (t) =>
      isObserveOnlyThreshold(t['http_req_duration{name:tv-season-1}'], 'p(95)<3500'),
    'discover name threshold is observability-only': (t) =>
      isObserveOnlyThreshold(t['http_req_duration{name:discover}'], 'p(95)<4000'),
    'explore-preview name threshold is observability-only': (t) =>
      isObserveOnlyThreshold(t['http_req_duration{name:explore-preview}'], 'p(95)<4000'),
    'personalized name threshold is observability-only': (t) =>
      isObserveOnlyThreshold(t['http_req_duration{name:personalized}'], 'p(95)<5000'),
    'recommendations-home name threshold is observability-only': (t) =>
      isObserveOnlyThreshold(t['http_req_duration{name:recommendations-home}'], 'p(95)<5000'),
    'discover group gate preserved': (t) =>
      isGatingThreshold(t['http_req_duration{group:discover}'], 'p(95)<4000'),
    'personalized group gate preserved': (t) =>
      isGatingThreshold(t['http_req_duration{group:personalized}'], 'p(95)<5000'),
    'movie-detail group threshold preserved': (t) =>
      Array.isArray(t['http_req_duration{group:movie-detail}']),
  });
}
