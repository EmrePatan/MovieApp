import { check } from 'k6';
import { Expectation, classifyResponse } from '../lib/expectations.js';
import '../lib/metrics.js';

export const options = {
  vus: 1,
  iterations: 1,
};

export default function semanticsSelfCheck() {
  const ratingAbsent = classifyResponse(404, Expectation.USER_STATE_ABSENT_404);
  check(ratingAbsent, {
    'ratings/me absent 404 is semantic success': (c) => c.semanticSuccess && !c.unexpected,
  });

  const catalogMissing = classifyResponse(404, Expectation.API_SUCCESS);
  check(catalogMissing, {
    'catalog 404 is unexpected': (c) => !c.semanticSuccess && c.unexpected,
  });

  const searchLimited = classifyResponse(429, Expectation.RATE_LIMIT_AWARE);
  check(searchLimited, {
    'search 429 is semantic success but rate limited': (c) =>
      c.semanticSuccess && c.rateLimited && !c.unexpected,
  });

  const unauthorized = classifyResponse(401, Expectation.API_SUCCESS);
  check(unauthorized, {
    '401 remains failure': (c) => !c.semanticSuccess && c.unexpected,
  });
}
