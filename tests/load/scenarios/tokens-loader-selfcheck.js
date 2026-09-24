import { check } from 'k6';
import { stripUtf8Bom, parseJsonOpen } from '../lib/jsonText.js';
import { identityPool } from '../lib/identities.js';
import { loadContentPools } from '../lib/config.js';

const contentPools = loadContentPools();
const bomWrappedIdentities = JSON.parse(stripUtf8Bom('\uFEFF{"identities":[{"id":"bom","bearerToken":"ok"}]}'));
let malformedJsonRejected = false;
try {
  JSON.parse('{');
} catch (_) {
  malformedJsonRejected = true;
}

export const options = {
  vus: 1,
  iterations: 1,
};

export default function tokensLoaderSelfCheck() {
  const bomSample = '\uFEFF{"identities":[]}';
  const stripped = stripUtf8Bom(bomSample);
  check(null, {
    'stripUtf8Bom removes leading BOM': () => stripped.startsWith('{'),
  });

  check(contentPools, {
    'hot content pool has movie IDs': (p) => (p.movieIds?.length || 0) > 0,
  });

  const tokensFile = __ENV.LOAD_TEST_TOKENS_FILE || '';
  check(null, {
    'UTF-8 BOM JSON parses after strip': () => bomWrappedIdentities.identities?.length === 1,
    'malformed JSON fails parse': () => malformedJsonRejected,
    'identity pool loaded when tokens configured': () => identityPool.length >= 1,
    'tokens file path has no backslash escapes': () => !tokensFile.includes('\\'),
  });
}
