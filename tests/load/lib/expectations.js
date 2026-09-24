import http from 'k6/http';

/**
 * Endpoint-aware HTTP semantics for Movie Cave load tests.
 * See tests/load/docs/expected-status.md
 */
export const Expectation = {
  /** Standard authenticated or public read — 2xx only. */
  API_SUCCESS: 'api_success',
  /** User-specific state that may be absent — 200 or 404 (e.g. ratings/me). */
  USER_STATE_ABSENT_404: 'user_state_absent_404',
  /** Search under per-IP rate limits — 2xx success; 429 tracked, not a backend saturation signal. */
  RATE_LIMIT_AWARE: 'rate_limit_aware',
  /** Control-plane probe — excluded from application capacity metrics via workload_scope tag. */
  CONTROL_PLANE: 'control_plane',
};

export function responseCallbackFor(expectation) {
  switch (expectation) {
    case Expectation.USER_STATE_ABSENT_404:
      return http.expectedStatuses(200, 404);
    case Expectation.RATE_LIMIT_AWARE:
      return http.expectedStatuses(200, 429);
    case Expectation.CONTROL_PLANE:
    case Expectation.API_SUCCESS:
    default:
      return http.expectedStatuses({ min: 200, max: 299 });
  }
}

export function classifyResponse(status, expectation) {
  if (status === 0) {
    return { semanticSuccess: false, outcome: 'timeout', rateLimited: false, unexpected: true };
  }
  if (status >= 500) {
    return { semanticSuccess: false, outcome: 'server_error', rateLimited: false, unexpected: true };
  }
  if (status === 401) {
    return { semanticSuccess: false, outcome: 'auth_401', rateLimited: false, unexpected: true };
  }
  if (status === 403) {
    return { semanticSuccess: false, outcome: 'auth_403', rateLimited: false, unexpected: true };
  }
  if (status === 429) {
    if (expectation === Expectation.RATE_LIMIT_AWARE) {
      return { semanticSuccess: true, outcome: 'rate_limited', rateLimited: true, unexpected: false };
    }
    return { semanticSuccess: false, outcome: 'rate_limited', rateLimited: true, unexpected: true };
  }
  if (status === 404) {
    if (expectation === Expectation.USER_STATE_ABSENT_404) {
      return { semanticSuccess: true, outcome: 'state_absent', rateLimited: false, unexpected: false };
    }
    return { semanticSuccess: false, outcome: 'not_found', rateLimited: false, unexpected: true };
  }
  if (status >= 200 && status < 300) {
    return { semanticSuccess: true, outcome: 'success', rateLimited: false, unexpected: false };
  }
  return { semanticSuccess: false, outcome: 'unexpected_4xx', rateLimited: false, unexpected: true };
}
