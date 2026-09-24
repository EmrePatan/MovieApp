import { Rate, Counter } from 'k6/metrics';

/** True when the response matches endpoint-aware semantics (includes state-absent 404). */
export const semanticSuccess = new Rate('semantic_success');

/** True when status is not semantically acceptable for the endpoint class. */
export const unexpectedStatus = new Rate('unexpected_status');

/** Count of HTTP 429 responses (search / rate-limit-aware endpoints). */
export const rateLimited = new Rate('rate_limited');

export const rateLimitedCount = new Counter('rate_limited_count');

export const outcome2xxSuccess = new Counter('http_outcome_2xx_success');
export const outcomeStateAbsent404 = new Counter('http_outcome_state_absent_404');
export const outcomeUnexpected4xx = new Counter('http_outcome_unexpected_4xx');
export const outcome401 = new Counter('http_outcome_401');
export const outcome403 = new Counter('http_outcome_403');
export const outcome429 = new Counter('http_outcome_429');
export const outcome5xx = new Counter('http_outcome_5xx');
export const outcomeTransportTimeout = new Counter('http_outcome_transport_timeout');

export function recordHttpSemantics(classification) {
  semanticSuccess.add(classification.semanticSuccess);
  unexpectedStatus.add(classification.unexpected);
  if (classification.rateLimited) {
    rateLimited.add(1);
    rateLimitedCount.add(1);
  } else {
    rateLimited.add(0);
  }

  switch (classification.outcome) {
    case 'success':
      outcome2xxSuccess.add(1);
      break;
    case 'state_absent':
      outcomeStateAbsent404.add(1);
      break;
    case 'timeout':
      outcomeTransportTimeout.add(1);
      break;
    case 'server_error':
      outcome5xx.add(1);
      break;
    case 'auth_401':
      outcome401.add(1);
      break;
    case 'auth_403':
      outcome403.add(1);
      break;
    case 'rate_limited':
      outcome429.add(1);
      break;
    case 'unexpected_4xx':
    case 'not_found':
      outcomeUnexpected4xx.add(1);
      break;
    default:
      break;
  }
}
