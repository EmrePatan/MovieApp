import { check } from 'k6';
import { assembleShardedIdentitiesPayload } from '../lib/identitiesShardParsing.js';
import { isGrafanaSecretsTransport } from '../lib/identitiesGrafanaSecrets.js';
import { identityForVu } from '../lib/content.js';
import { setRuntimeIdentityPool } from '../lib/identitiesRuntime.js';

const MAX_PART_UTF8_BYTES = 22528;

function utf8ByteLength(text) {
  return new TextEncoder().encode(text).length;
}

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

function greedyUtf8Parts(identities, maxBytes) {
  const encoded = identities.map((item) => JSON.stringify(item));
  const prefix = '{"identities":[';
  const suffix = ']}';
  const parts = [];
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

    if (utf8ByteLength(withClose) <= maxBytes) {
      if (isLast) {
        parts.push(withClose);
        current = null;
      } else {
        current = candidate;
      }
      i += 1;
      continue;
    }

    if (!current) {
      throw new Error(`identity ${i} exceeds utf8 part limit`);
    }
    parts.push(current);
    current = null;
  }

  return parts;
}

export const options = {
  vus: 1,
  iterations: 1,
};

export default function identitiesGrafanaSecretsSelfCheck() {
  const hundred = makeFakeIdentities(100);
  const parts = greedyUtf8Parts(hundred, MAX_PART_UTF8_BYTES);
  const reassembled = assembleShardedIdentitiesPayload(parts);

  check(null, {
    '100 identities reassemble from utf8 parts': () => reassembled.length === 100,
    'utf8 parts under 22 KiB': () => parts.every((part) => utf8ByteLength(part) <= MAX_PART_UTF8_BYTES),
    'order preserved': () => reassembled[0].id === 'load60-1' && reassembled[99].id === 'load60-100',
    'grafana-secrets transport detects env': () => isGrafanaSecretsTransport({ LOAD_TEST_IDENTITIES_TRANSPORT: 'grafana-secrets' }),
  });

  let missingPartFailed = false;
  try {
    assembleShardedIdentitiesPayload(['{"identities":[']);
  } catch (_) {
    missingPartFailed = true;
  }
  check(null, { 'malformed payload fails': () => missingPartFailed });

  const runtimePool = [
    { id: 'vu-a', bearerToken: 'token-a' },
    { id: 'vu-b', bearerToken: 'token-b' },
  ];
  setRuntimeIdentityPool(runtimePool);
  const first = identityForVu(1);
  const wrapped = identityForVu(3);
  check(null, {
    'vu modulo mapping via runtime pool': () => first.id === wrapped.id,
  });
}
