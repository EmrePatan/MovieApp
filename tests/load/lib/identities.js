import { SharedArray } from 'k6/data';
import { dataPath } from './config.js';
import { parseJsonOpen } from './jsonText.js';

function loadFromIdentitiesJsonEnv() {
  const raw = __ENV.LOAD_TEST_IDENTITIES_JSON;
  if (!raw || raw.trim() === '') {
    return null;
  }
  const parsed = JSON.parse(raw);
  const source = Array.isArray(parsed) ? parsed : parsed.identities || [];
  const list = source.filter((i) => i?.bearerToken && i.bearerToken !== 'REPLACE_WITH_JWT');
  return list.length > 0 ? list : null;
}

export const identityPool = new SharedArray('identities', function loadIdentities() {
  const fromEnvJson = loadFromIdentitiesJsonEnv();
  if (fromEnvJson) {
    return fromEnvJson;
  }

  const file = __ENV.LOAD_TEST_TOKENS_FILE;
  if (file) {
    const parsed = parseJsonOpen(file);
    return (parsed.identities || []).filter((i) => i.bearerToken && i.bearerToken !== 'REPLACE_WITH_JWT');
  }

  const inline = [];
  for (let i = 1; i <= 200; i += 1) {
    const token = __ENV[`LOAD_TEST_TOKEN_${String(i).padStart(3, '0')}`];
    if (token) {
      inline.push({ id: `env-token-${i}`, bearerToken: token });
    }
  }
  if (inline.length > 0) {
    return inline;
  }

  try {
    const parsed = parseJsonOpen(dataPath('tokens.json'));
    return (parsed.identities || []).filter((i) => i.bearerToken && i.bearerToken !== 'REPLACE_WITH_JWT');
  } catch (_) {
    return [];
  }
});

function resolveVuId(vu) {
  if (typeof vu === 'number' && vu > 0) {
    return vu;
  }
  return __VU;
}

export function requireIdentityForVu(vu) {
  const vuId = resolveVuId(vu);
  const identity = identityPool.length === 0 ? null : identityPool[(vuId - 1) % identityPool.length];
  if (!identity?.bearerToken) {
    throw new Error('User-concurrency scenario requires at least one valid bearer token identity');
  }
  return identity;
}
