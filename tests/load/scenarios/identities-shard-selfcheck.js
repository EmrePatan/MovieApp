import { check } from 'k6';
import {
  assembleShardedIdentitiesPayload,
  loadIdentitiesFromShardEnv,
  parseIdentitiesPayload,
} from '../lib/identitiesShardParsing.js';
import { identityPool, requireIdentityForVu } from '../lib/identities.js';

function makeFakeIdentities(count) {
  const list = [];
  for (let i = 1; i <= count; i += 1) {
    list.push({
      id: `load60-${i}`,
      bearerToken: `eyJ.fake.${String(i).padStart(4, '0')}.signature`,
    });
  }
  return list;
}

function greedyShardParts(identities, maxChars) {
  const encoded = identities.map((item) => JSON.stringify(item));
  const prefix = '{"identities":[';
  const suffix = ']}';
  const shards = [];
  let current = null;
  let i = 0;

  while (i < encoded.length) {
    const enc = encoded[i];
    let candidate;
    if (current === null) {
      candidate = i === 0 ? prefix + enc : `,${enc}`;
    } else {
      candidate = `${current},${enc}`;
    }

    const isLast = i === encoded.length - 1;
    const withClose = isLast ? candidate + suffix : candidate;

    if (withClose.length <= maxChars) {
      if (isLast) {
        shards.push(withClose);
        current = null;
      } else {
        current = candidate;
      }
      i += 1;
      continue;
    }

    if (!current) {
      throw new Error(`identity ${i} exceeds shard limit`);
    }
    shards.push(current);
    current = null;
  }

  return shards;
}

export const options = {
  vus: 1,
  iterations: 1,
};

export default function identitiesShardSelfCheck() {
  const hundred = makeFakeIdentities(100);
  const shardParts = greedyShardParts(hundred, 4500);
  const reassembled = assembleShardedIdentitiesPayload(shardParts);

  check(null, {
    '100 identities reassemble': () => reassembled.length === 100,
    'order preserved': () => reassembled[0].id === 'load60-1' && reassembled[99].id === 'load60-100',
    'all shards under limit': () => shardParts.every((part) => part.length <= 4500),
  });

  const env = {};
  shardParts.forEach((part, index) => {
    env[`LOAD_TEST_IDENTITIES_JSON_${String(index + 1).padStart(3, '0')}`] = part;
  });
  const fromEnv = loadIdentitiesFromShardEnv(env, shardParts.length);
  check(null, {
    'shard env loader count': () => fromEnv.length === 100,
  });

  let missingFailed = false;
  try {
    loadIdentitiesFromShardEnv({ LOAD_TEST_IDENTITIES_JSON_001: shardParts[0] }, 2);
  } catch (_) {
    missingFailed = true;
  }
  check(null, { 'missing shard fails': () => missingFailed });

  let malformedFailed = false;
  try {
    parseIdentitiesPayload('{"identities":[}');
  } catch (_) {
    malformedFailed = true;
  }
  check(null, { 'malformed json fails': () => malformedFailed });

  const singleRaw = __ENV.LOAD_TEST_IDENTITIES_SELFTEST_SINGLE;
  if (singleRaw) {
    const single = parseIdentitiesPayload(singleRaw);
    check(null, { 'single env payload works': () => single.length >= 1 });
  }

  if (identityPool.length > 0) {
    const a = requireIdentityForVu(1);
    const b = requireIdentityForVu(identityPool.length + 1);
    check(null, {
      'vu modulo mapping unchanged': () => a.id === b.id,
    });
  }
}
