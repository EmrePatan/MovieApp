import { check } from 'k6';
import { buildEnhancedReport, TRACKED_GROUPS } from '../lib/summaryReport.js';

export const options = {
  vus: 1,
  iterations: 1,
};

export default function summaryReportSelfCheck() {
  const mockData = {
    metrics: {
      http_reqs: { values: { count: 100, rate: 10 } },
      'http_reqs{group:home,workload_scope:application}': { values: { count: 40, rate: 4 } },
      'http_req_duration{group:home}': {
        values: { med: 200, 'p(90)': 400, 'p(95)': 500, 'p(99)': 800, max: 1200 },
      },
      http_req_duration: { values: { med: 220, 'p(95)': 850, max: 1500 } },
      http_outcome_2xx_success: { values: { count: 90 } },
      http_outcome_transport_timeout: { values: { count: 5 } },
      http_outcome_state_absent_404: { values: { count: 5 } },
      iterations: { values: { count: 50, rate: 5 } },
      interrupted_iterations: { values: { count: 2, rate: 0.2 } },
      'http_reqs{workload_scope:application}': { values: { count: 95, rate: 9.5 } },
    },
    root_group: {
      checks: [
        {
          name: 'home semantic_success',
          passes: 38,
          fails: 2,
        },
        {
          name: 'home not_unexpected_status',
          passes: 38,
          fails: 2,
        },
      ],
      groups: [],
    },
  };

  const report = buildEnhancedReport(mockData, { reportSchemaVersion: 2, scenario: 'selfcheck' });

  check(report, {
    'schema version present': (r) => r.metadata.reportSchemaVersion === 2,
    'outcomes include timeout count': (r) => r.outcomes.transportTimeout.count === 5,
    'home group latency p50': (r) => r.groups.home?.latency?.p50 === 200,
    'home group latency p95': (r) => r.groups.home?.latency?.['p(95)'] === 500,
    'home semantic rate': (r) => r.groups.home?.checks?.semanticSuccess?.rate === 0.95,
    'application RPS': (r) => r.application.http_reqs.rate === 9.5,
    'interrupted iterations': (r) => r.iterations.interrupted.count === 2,
    'tracked groups list non-empty': () => TRACKED_GROUPS.length >= 10,
  });
}
