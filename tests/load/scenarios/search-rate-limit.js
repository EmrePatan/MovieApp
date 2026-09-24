import { currentVu, currentIteration } from '../lib/executionContext.js';
import {
  buildStagesFromPreset,
  presetName,
  stageTarget,
  environmentLabel,
} from '../lib/config.js';
import { loadThresholds } from '../lib/thresholds.js';
import { handleSummaryFactory } from '../lib/summary.js';
import { pickSearchTerm } from '../lib/content.js';
import { apiGet, Expectation } from '../lib/http.js';
import '../lib/metrics.js';

/**
 * Separate profile to exercise per-IP search rate limits without polluting
 * main user-concurrency / capacity results. Run manually at low VU / controlled RPS.
 *
 * Do NOT run at 500–1000 VUs from a single public IP — results measure rate limiting, not capacity.
 */
const presets = JSON.parse(open('../config/presets.json'));

export const options = {
  scenarios: {
    search_rate_limit_probe: {
      executor: 'ramping-vus',
      stages: buildStagesFromPreset(presets),
      gracefulRampDown: '15s',
    },
  },
  thresholds: loadThresholds(),
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
  tags: {
    test_type: 'search-rate-limit',
    preset: presetName(),
    environment: environmentLabel(),
  },
};

export default function searchRateLimitProbe() {
  const vu = currentVu();
  const iter = currentIteration();
  const seed = vu * 1000 + iter;
  const term = pickSearchTerm(seed);
  const mode = seed % 3;

  if (mode === 0) {
    apiGet(`/api/search?q=${encodeURIComponent(term)}&type=all&page=1&pageSize=20`, {
      group: 'search',
      name: 'unified-search',
      expectation: Expectation.RATE_LIMIT_AWARE,
    });
  } else if (mode === 1) {
    apiGet(`/api/movies/search?q=${encodeURIComponent(term)}&page=1&pageSize=20`, {
      group: 'search',
      name: 'movie-search',
      expectation: Expectation.RATE_LIMIT_AWARE,
    });
  } else {
    apiGet(`/api/tvshows/search?q=${encodeURIComponent(term)}&page=1&pageSize=20`, {
      group: 'search',
      name: 'tv-search',
      expectation: Expectation.RATE_LIMIT_AWARE,
    });
  }
}

export function handleSummary(data) {
  return handleSummaryFactory({
    testType: 'search-rate-limit',
    stageTargetVus: stageTarget(),
    note: 'Interpret rate_limited metric separately from backend saturation. Single-IP high-VU runs are invalid for this scenario.',
  })(data);
}
