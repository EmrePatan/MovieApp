import { scenario } from 'k6/execution';
import { buildStagesFromPreset, presetName, stageTarget } from '../lib/config.js';
import { loadThresholds } from '../lib/thresholds.js';
import { handleSummaryFactory } from '../lib/summary.js';
import { identityForVu, pickTvShowId } from '../lib/content.js';
import { apiPostJson } from '../lib/http.js';

/**
 * STAGING / LOCAL ONLY — worst-case TV bulk watch mutation.
 * NOT part of production user-concurrency mix.
 *
 * Requires LOAD_TEST_ALLOW_BULK_WATCH=true and a dedicated staging TV show ID
 * with a known episode graph. Never run against production.
 */
const presets = JSON.parse(open('../config/presets.json'));

if ((__ENV.LOAD_TEST_ALLOW_BULK_WATCH || 'false').toLowerCase() !== 'true') {
  throw new Error('benchmark-tv-bulk-watch.js requires LOAD_TEST_ALLOW_BULK_WATCH=true');
}

export const options = {
  scenarios: {
    tv_bulk_watch: {
      executor: 'ramping-vus',
      stages: buildStagesFromPreset(presets),
      gracefulRampDown: '30s',
    },
  },
  thresholds: loadThresholds(),
  tags: {
    test_type: 'benchmark-tv-bulk-watch',
    preset: presetName(),
  },
};

export default function tvBulkWatch() {
  const vu = scenario.vuIdInTest;
  const iter = scenario.iterationInTest;
  const identity = identityForVu(vu);
  if (!identity) {
    throw new Error('Bulk watch benchmark requires bearer tokens');
  }

  const tvShowId = __ENV.LOAD_TEST_BULK_TV_SHOW_ID || pickTvShowId(vu + iter);
  const episodeIdsRaw = __ENV.LOAD_TEST_BULK_EPISODE_IDS;
  if (!episodeIdsRaw) {
    throw new Error('LOAD_TEST_BULK_EPISODE_IDS is required (JSON array of episode GUIDs)');
  }
  const episodeIds = JSON.parse(episodeIdsRaw);
  apiPostJson(
    `/api/watch-history/tvshows/${tvShowId}/episodes/bulk`,
    { episodeIds, watched: true },
    { group: 'tv-bulk-watch', token: identity.bearerToken },
  );
}

export function handleSummary(data) {
  return handleSummaryFactory({
    testType: 'benchmark-tv-bulk-watch',
    stageTargetVus: stageTarget(),
    warning: 'Mutation benchmark — staging only',
  })(data);
}
