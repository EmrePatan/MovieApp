import { currentVu, currentIteration } from '../lib/executionContext.js';
import {
  buildStagesFromPreset,
  presetName,
  stageTarget,
  contentDataset,
  environmentLabel,
  backendCommitSha,
  loadTestCommitSha,
  searchProfile,
  includeExternalRatings,
} from '../lib/config.js';
import { loadThresholds } from '../lib/thresholds.js';
import { handleSummaryFactory } from '../lib/summary.js';
import { identityForVu, contentPoolStats } from '../lib/content.js';
import { isGrafanaSecretsTransport, loadIdentitiesFromGrafanaSecrets } from '../lib/identitiesGrafanaSecrets.js';
import { setRuntimeIdentityPool } from '../lib/identitiesRuntime.js';
import { runUserJourney } from '../lib/journey.js';
import { thinkBetweenIterations } from '../lib/thinktime.js';
import { applyCloudOptions } from '../lib/cloudOptions.js';
import '../lib/metrics.js';

const presets = JSON.parse(open('../config/presets.json'));

const optionsBase = {
  scenarios: {
    movie_cave_users: {
      executor: 'ramping-vus',
      stages: buildStagesFromPreset(presets),
      gracefulRampDown: '30s',
    },
  },
  thresholds: loadThresholds(),
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
  tags: {
    test_type: 'user-concurrency',
    preset: presetName(),
    content_dataset: contentDataset(),
    environment: environmentLabel(),
  },
};

export const options = applyCloudOptions(optionsBase);

export async function setup() {
  if (!isGrafanaSecretsTransport()) {
    return { identityTransport: 'env', identityCount: null };
  }

  const identities = await loadIdentitiesFromGrafanaSecrets();
  setRuntimeIdentityPool(identities);
  return {
    identityTransport: 'grafana-secrets',
    identityCount: identities.length,
  };
}

export default function userConcurrency() {
  const vu = currentVu();
  const iter = currentIteration();
  const identity = identityForVu(vu);
  runUserJourney({ vu, iter, token: identity?.bearerToken });
  thinkBetweenIterations();
}

function executionModeLabel() {
  const mode = (__ENV.LOAD_TEST_EXECUTION_MODE || 'local').toLowerCase();
  return mode === 'grafana-cloud' ? 'grafana-cloud' : 'local';
}

function identityReuseRatio(stageVus, identityCount) {
  if (!identityCount || identityCount < 1) {
    return null;
  }
  if (!stageVus || stageVus < 1) {
    return null;
  }
  return stageVus / identityCount;
}

export function handleSummary(data) {
  const stage = stageTarget();
  const pool = contentPoolStats();
  return handleSummaryFactory({
    testType: 'user-concurrency',
    stageTargetVus: stage,
    contentPool: pool,
    searchProfile: searchProfile(),
    includeExternalRatings: includeExternalRatings(),
    backendCommitSha: backendCommitSha(),
    loadTestCommitSha: loadTestCommitSha(),
    executionMode: executionModeLabel(),
    identityCount: pool.identityCount,
    identityTransport: pool.identityTransport,
    identityReuseRatio: identityReuseRatio(stage, pool.identityCount),
    cloudLoadZone: __ENV.LOAD_TEST_CLOUD_LOAD_ZONE || null,
  })(data);
}
