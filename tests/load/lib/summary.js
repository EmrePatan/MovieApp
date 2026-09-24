import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.4/index.js';
import { buildEnhancedReport } from './summaryReport.js';

export function buildRunMetadata(extra = {}) {
  const executionModeRaw = (__ENV.LOAD_TEST_EXECUTION_MODE || 'local').toLowerCase();
  const executionMode = executionModeRaw === 'grafana-cloud' ? 'grafana-cloud' : 'local';
  return {
    timestampUtc: new Date().toISOString(),
    environment: __ENV.LOAD_TEST_ENVIRONMENT || 'unspecified',
    baseUrl: __ENV.LOAD_TEST_BASE_URL || '',
    backendCommitSha: __ENV.LOAD_TEST_BACKEND_SHA || 'unknown',
    loadTestCommitSha: __ENV.LOAD_TEST_TOOL_SHA || 'unknown',
    scenario: __ENV.LOAD_TEST_SCENARIO || 'unknown',
    preset: __ENV.LOAD_TEST_PRESET || 'smoke',
    stageTargetVus: __ENV.LOAD_TEST_STAGE_TARGET || __ENV.LOAD_TEST_VUS || '',
    contentDataset: __ENV.LOAD_TEST_CONTENT_DATASET || 'hot',
    executionMode,
    reportSchemaVersion: 2,
    ...extra,
  };
}

export function handleSummaryFactory(extraMetadata = {}) {
  return function handleSummary(data) {
    const metadata = buildRunMetadata(extraMetadata);
    const report = buildEnhancedReport(data, metadata);

    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    const scenario = __ENV.LOAD_TEST_SCENARIO || 'run';
    const outPath = __ENV.LOAD_TEST_REPORT_PATH || `tests/load/reports/${scenario}-${stamp}.json`;
    const isCloud = (__ENV.LOAD_TEST_EXECUTION_MODE || '').toLowerCase() === 'grafana-cloud';
    const summaryOut = {
      stdout: textSummary(data, { indent: ' ', enableColors: true }),
    };

    // Grafana Cloud workers may not persist arbitrary filesystem paths; still emit v2 JSON locally when configured.
    if (!isCloud || __ENV.LOAD_TEST_REPORT_PATH) {
      summaryOut[outPath] = JSON.stringify(report, null, 2);
    }

    return summaryOut;
  };
}
