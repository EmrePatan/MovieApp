import { apiGet, Expectation } from '../lib/http.js';
import { handleSummaryFactory } from '../lib/summary.js';
import '../lib/metrics.js';

/**
 * Operator preflight — liveness/readiness only. Not part of application capacity throughput.
 */
export const options = {
  vus: 1,
  iterations: 1,
  thresholds: {
    unexpected_status: ['rate==0'],
  },
};

export default function preflightHealth() {
  apiGet('/health/live', {
    group: 'preflight-health',
    name: 'health-live',
    expectation: Expectation.CONTROL_PLANE,
    workloadScope: 'control',
  });
  apiGet('/health/ready', {
    group: 'preflight-health',
    name: 'health-ready',
    expectation: Expectation.CONTROL_PLANE,
    workloadScope: 'control',
  });
}

export function handleSummary(data) {
  return handleSummaryFactory({
    testType: 'preflight-health',
    workloadScope: 'control',
  })(data);
}
