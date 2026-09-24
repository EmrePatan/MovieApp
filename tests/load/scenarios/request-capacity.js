import { scenario } from 'k6/execution';
import {
  buildStagesFromPreset,
  presetName,
  stageTarget,
  contentDataset,
  environmentLabel,
} from '../lib/config.js';
import { loadThresholds } from '../lib/thresholds.js';
import { handleSummaryFactory } from '../lib/summary.js';
import { pickMovieId, pickTvShowId, contentPoolStats } from '../lib/content.js';
import { apiGet } from '../lib/http.js';

/**
 * READ-ONLY saturation profile — no auth, no writes, no search (IP rate limits),
 * no external-rating refresh paths. Uses catalog-backed GETs and health probes only.
 */
const presets = JSON.parse(open('../config/presets.json'));

export const options = {
  scenarios: {
    request_capacity: {
      executor: 'ramping-vus',
      stages: buildStagesFromPreset(presets),
      gracefulRampDown: '30s',
    },
  },
  thresholds: loadThresholds(),
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
  tags: {
    test_type: 'request-capacity',
    preset: presetName(),
    content_dataset: contentDataset(),
    environment: environmentLabel(),
  },
};

export default function requestCapacity() {
  const vu = scenario.vuIdInTest;
  const iter = scenario.iterationInTest;
  const seed = vu * 1000 + iter;
  const bucket = seed % 5;

  switch (bucket) {
    case 0:
      apiGet('/health/live', { group: 'saturation-health' });
      break;
    case 1:
      apiGet('/api/discovery/trending?type=all&page=1&pageSize=20', { group: 'saturation-discovery' });
      break;
    case 2:
      apiGet('/api/discovery/explore-preview?sectionSize=10', {
        group: 'saturation-discovery',
        name: 'explore-preview',
      });
      break;
    case 3: {
      const movieId = pickMovieId(seed);
      apiGet(`/api/movies/${movieId}`, { group: 'saturation-catalog' });
      if (Math.random() < 0.4) {
        apiGet(`/api/movies/${movieId}/credits`, { group: 'saturation-catalog', name: 'movie-credits' });
      }
      break;
    }
    default: {
      const tvId = pickTvShowId(seed);
      apiGet(`/api/tvshows/${tvId}`, { group: 'saturation-catalog' });
      break;
    }
  }
}

export function handleSummary(data) {
  return handleSummaryFactory({
    testType: 'request-capacity',
    stageTargetVus: stageTarget(),
    contentPool: contentPoolStats(),
    note: 'Saturation profile excludes unified search and external provider cache misses by design.',
  })(data);
}
