import { Rate, Counter } from 'k6/metrics';

/** True when the response matches endpoint-aware semantics (includes state-absent 404). */
export const semanticSuccess = new Rate('semantic_success');

/** True when status is not semantically acceptable for the endpoint class. */
export const unexpectedStatus = new Rate('unexpected_status');

/** Count of HTTP 429 responses (search / rate-limit-aware endpoints). */
export const rateLimited = new Rate('rate_limited');

export const rateLimitedCount = new Counter('rate_limited_count');

export function recordHttpSemantics(classification) {
  semanticSuccess.add(classification.semanticSuccess);
  unexpectedStatus.add(classification.unexpected);
  if (classification.rateLimited) {
    rateLimited.add(1);
    rateLimitedCount.add(1);
  } else {
    rateLimited.add(0);
  }
}
