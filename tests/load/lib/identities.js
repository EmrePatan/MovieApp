import { SharedArray } from 'k6/data';
import { dataPath } from './config.js';
import { parseJsonOpen } from './jsonText.js';
import {
  loadIdentitiesFromShardEnv,
  normalizeIdentityList,
  parseIdentitiesPayload,
} from './identitiesShardParsing.js';

function loadFromIdentitiesJsonEnv() {
  const raw = __ENV.LOAD_TEST_IDENTITIES_JSON;
  if (!raw || raw.trim() === '') {
    return null;
  }
  return parseIdentitiesPayload(raw);
}

function loadFromShardedIdentitiesEnv() {
  const countRaw = __ENV.LOAD_TEST_IDENTITIES_SHARD_COUNT;
  if (!countRaw || countRaw.trim() === '') {
    return null;
  }

  const shardCount = parseInt(countRaw, 10);
  if (Number.isNaN(shardCount)) {
    throw new Error(`Invalid LOAD_TEST_IDENTITIES_SHARD_COUNT: ${countRaw}`);
  }

  return loadIdentitiesFromShardEnv(__ENV, shardCount);
}

export const identityPool = new SharedArray('identities', function loadIdentities() {
  if ((__ENV.LOAD_TEST_IDENTITIES_TRANSPORT || '').toLowerCase() === 'grafana-secrets') {
    return [];
  }

  const fromEnvJson = loadFromIdentitiesJsonEnv();
  if (fromEnvJson) {
    return fromEnvJson;
  }

  const fromShards = loadFromShardedIdentitiesEnv();
  if (fromShards) {
    return fromShards;
  }

  const file = __ENV.LOAD_TEST_TOKENS_FILE;
  if (file) {
    const parsed = parseJsonOpen(file);
    const list = normalizeIdentityList(parsed);
    return list || [];
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
    const list = normalizeIdentityList(parsed);
    return list || [];
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
