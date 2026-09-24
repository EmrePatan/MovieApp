import { check } from 'k6';
import { loadThresholds } from '../lib/thresholds.js';

export const options = {
  vus: 1,
  iterations: 1,
};

export default function thresholdsConfigSelfCheck() {
  const thresholds = loadThresholds();

  check(thresholds, {
    'tv show detail gated by name tag': (t) =>
      Array.isArray(t['http_req_duration{name:tv-detail}']) &&
      t['http_req_duration{name:tv-detail}'][0] === 'p(95)<3500',
    'tv show detail not gated by combined group tag': (t) =>
      t['http_req_duration{group:tv-detail}'] === undefined,
    'tv season-1 threshold is observability-only': (t) => {
      const entry = t['http_req_duration{name:tv-season-1}'];
      return (
        Array.isArray(entry) &&
        entry.length === 1 &&
        entry[0].threshold === 'p(95)<3500' &&
        entry[0].abortOnFail === false
      );
    },
    'movie-detail group threshold preserved': (t) =>
      Array.isArray(t['http_req_duration{group:movie-detail}']),
  });
}
