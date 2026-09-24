import { check } from 'k6';
import { buildEnhancedReport, TRACKED_GROUPS, TRACKED_REQUEST_NAMES } from '../lib/summaryReport.js';

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
      'http_req_duration{name:tv-detail}': {
        values: { med: 300, 'p(95)': 900, max: 1100 },
      },
      'http_req_duration{name:tv-season-1}': {
        values: { med: 1200, 'p(95)': 4200, max: 5000 },
      },
      'http_reqs{name:tv-detail}': { values: { count: 12, rate: 1.2 } },
      'http_reqs{name:tv-season-1}': { values: { count: 3, rate: 0.3 } },
      'http_req_duration{name:discover}': {
        values: { med: 400, 'p(95)': 1200, max: 2000 },
      },
      'http_req_duration{name:explore-preview}': {
        values: { med: 900, 'p(95)': 8100, max: 9000 },
      },
      'http_req_duration{name:personalized}': {
        values: { med: 500, 'p(95)': 2000, max: 3000 },
      },
      'http_req_duration{name:recommendations-home}': {
        values: { med: 800, 'p(95)': 11600, max: 12000 },
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
    'tv-detail request name latency': (r) => r.requestNames['tv-detail']?.latency?.['p(95)'] === 900,
    'tv-season-1 request name latency': (r) => r.requestNames['tv-season-1']?.latency?.['p(95)'] === 4200,
    'tracked request names list': () => TRACKED_REQUEST_NAMES.includes('tv-season-1'),
    'discover request name latency': (r) => r.requestNames.discover?.latency?.['p(95)'] === 1200,
    'explore-preview request name latency': (r) =>
      r.requestNames['explore-preview']?.latency?.['p(95)'] === 8100,
    'personalized request name latency': (r) =>
      r.requestNames.personalized?.latency?.['p(95)'] === 2000,
    'recommendations-home request name latency': (r) =>
      r.requestNames['recommendations-home']?.latency?.['p(95)'] === 11600,
    'all smoke diagnostic request names tracked': () =>
      ['discover', 'explore-preview', 'personalized', 'recommendations-home'].every((name) =>
        TRACKED_REQUEST_NAMES.includes(name),
      ),
  });
}
