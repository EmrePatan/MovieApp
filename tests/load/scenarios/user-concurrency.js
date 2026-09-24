import { scenario } from 'k6/execution';
import {
  buildStagesFromPreset,
  presetName,
  stageTarget,
  contentDataset,
  environmentLabel,
  backendCommitSha,
  loadTestCommitSha,
} from '../lib/config.js';
import { loadThresholds } from '../lib/thresholds.js';
import { handleSummaryFactory } from '../lib/summary.js';
import { identityForVu, contentPoolStats } from '../lib/content.js';
import { runUserJourney } from '../lib/journey.js';
import { thinkBetweenIterations } from '../lib/thinktime.js';

const presets = JSON.parse(open('../config/presets.json'));

export const options = {
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

export default function userConcurrency() {
  const vu = scenario.vuIdInTest;
  const iter = scenario.iterationInTest;
  const identity = identityForVu(vu);
  runUserJourney({ vu, iter, token: identity?.bearerToken });
  thinkBetweenIterations();
}

export function handleSummary(data) {
  return handleSummaryFactory({
    testType: 'user-concurrency',
    stageTargetVus: stageTarget(),
    contentPool: contentPoolStats(),
    backendCommitSha: backendCommitSha(),
    loadTestCommitSha: loadTestCommitSha(),
  })(data);
}
