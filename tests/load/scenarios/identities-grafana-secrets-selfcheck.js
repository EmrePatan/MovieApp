import { check } from 'k6';
import { assembleShardedIdentitiesPayload } from '../lib/identitiesShardParsing.js';
import {
  applyGrafanaSecretsSetupToRuntime,
  buildGrafanaSecretsSetupResult,
  formatGrafanaSecretsRuntimeProof,
  isGrafanaSecretsTransport,
  resetGrafanaSecretsRuntimeApplyState,
} from '../lib/identitiesGrafanaSecrets.js';
import { identityForVu } from '../lib/content.js';
import { setRuntimeIdentityPool } from '../lib/identitiesRuntime.js';

const MAX_PART_UTF8_BYTES = 22528;
const GRAFANA_ENV = {
  LOAD_TEST_IDENTITIES_TRANSPORT: 'grafana-secrets',
  LOAD_TEST_IDENTITIES_SECRET_COUNT: '3',
  LOAD_TEST_IDENTITIES_SECRET_NAMES: 'movie-cave-load-identities-001,movie-cave-load-identities-002,movie-cave-load-identities-003',
  LOAD_TEST_EXPECTED_IDENTITY_COUNT: '100',
};

function utf8ByteLength(text) {
  return new TextEncoder().encode(text).length;
}

function makeFakeIdentities(count) {
  const list = [];
  for (let i = 1; i <= count; i += 1) {
    list.push({
      id: `load60-${String(i).padStart(3, '0')}`,
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

function expectThrows(fn) {
  try {
    fn();
    return false;
  } catch (_) {
    return true;
  }
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
    'order preserved': () => reassembled[0].id === 'load60-001' && reassembled[99].id === 'load60-100',
    'grafana-secrets transport detects env': () => isGrafanaSecretsTransport(GRAFANA_ENV),
  });

  check(null, {
    'malformed payload fails': () => expectThrows(() => assembleShardedIdentitiesPayload(['{"identities":['])),
  });

  const setupResult = buildGrafanaSecretsSetupResult(reassembled, GRAFANA_ENV);
  const proof = formatGrafanaSecretsRuntimeProof(setupResult);
  check(null, {
    'setup result carries 100 identities': () => setupResult.identities.length === 100,
    'runtime proof has no jwt material': () => !proof.includes('eyJ.'),
    'runtime proof lists transport and counts': () =>
      proof.includes('transport=grafana-secrets') &&
      proof.includes('identityCount=100') &&
      proof.includes('secretPartCount=3'),
  });

  resetGrafanaSecretsRuntimeApplyState();
  applyGrafanaSecretsSetupToRuntime(setupResult, GRAFANA_ENV);
  const vu1 = identityForVu(1);
  const vu3 = identityForVu(3);
  check(null, {
    'setup data -> runtime pool non-empty': () => Boolean(vu1?.bearerToken) && vu1.id === 'load60-001',
    'vu modulo mapping unchanged': () => identityForVu(101).id === vu1.id && vu3.id === 'load60-003',
  });

  check(null, {
    'expected count mismatch fails in setup build': () =>
      expectThrows(() =>
        buildGrafanaSecretsSetupResult(reassembled, {
          ...GRAFANA_ENV,
          LOAD_TEST_EXPECTED_IDENTITY_COUNT: '50',
        }),
      ),
    'empty setup identities fail at apply': () => {
      resetGrafanaSecretsRuntimeApplyState();
      return expectThrows(() =>
        applyGrafanaSecretsSetupToRuntime({ identities: [] }, GRAFANA_ENV),
      );
    },
    'missing setup identities fail at apply': () => {
      resetGrafanaSecretsRuntimeApplyState();
      return expectThrows(() => applyGrafanaSecretsSetupToRuntime({}, GRAFANA_ENV));
    },
  });

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
