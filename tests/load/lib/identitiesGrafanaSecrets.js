import secrets from 'k6/secrets';
import { assembleShardedIdentitiesPayload } from './identitiesShardParsing.js';
import { setRuntimeIdentityPool } from './identitiesRuntime.js';

export function isGrafanaSecretsTransport(env = __ENV) {
  return (env.LOAD_TEST_IDENTITIES_TRANSPORT || '').toLowerCase() === 'grafana-secrets';
}

export async function loadIdentitiesFromGrafanaSecrets(env = __ENV) {
  if (!isGrafanaSecretsTransport(env)) {
    return null;
  }

  const countRaw = env.LOAD_TEST_IDENTITIES_SECRET_COUNT;
  const count = parseInt(countRaw, 10);
  if (Number.isNaN(count) || count < 1) {
    throw new Error(`Invalid LOAD_TEST_IDENTITIES_SECRET_COUNT: ${countRaw}`);
  }

  const namesRaw = env.LOAD_TEST_IDENTITIES_SECRET_NAMES || '';
  const names = namesRaw
    .split(',')
    .map((name) => name.trim())
    .filter((name) => name.length > 0);
  if (names.length !== count) {
    throw new Error(
      `LOAD_TEST_IDENTITIES_SECRET_NAMES count (${names.length}) does not match LOAD_TEST_IDENTITIES_SECRET_COUNT (${count}).`,
    );
  }

  const parts = [];
  for (const name of names) {
    const value = await secrets.get(name);
    if (!value || value.trim() === '') {
      throw new Error(`Missing or empty Grafana secret: ${name}`);
    }
    parts.push(value);
  }

  const identities = assembleShardedIdentitiesPayload(parts);
  const expectedRaw = env.LOAD_TEST_EXPECTED_IDENTITY_COUNT;
  const expected = expectedRaw ? parseInt(expectedRaw, 10) : 0;
  if (!Number.isNaN(expected) && expected > 0 && identities.length !== expected) {
    throw new Error(`Identity count mismatch: expected ${expected}, reconstructed ${identities.length}.`);
  }

  if (identities.length < 1) {
    throw new Error('Grafana secrets transport reconstructed an empty identity pool.');
  }

  return identities;
}

/**
 * k6 setup() runs outside VU isolates — identities must be returned from setup()
 * and applied here before journey execution.
 */
let grafanaSecretsRuntimeApplied = false;

export function applyGrafanaSecretsSetupToRuntime(setupData, env = __ENV) {
  if (!isGrafanaSecretsTransport(env)) {
    return;
  }

  if (grafanaSecretsRuntimeApplied) {
    return;
  }

  const identities = setupData?.identities;
  if (!Array.isArray(identities) || identities.length < 1) {
    throw new Error(
      'Grafana secrets transport: setup() did not provide a non-empty identities array to VUs. ' +
        'Ensure setup() returns reconstructed identities from secrets.get().',
    );
  }

  const expectedRaw = env.LOAD_TEST_EXPECTED_IDENTITY_COUNT;
  const expected = expectedRaw ? parseInt(expectedRaw, 10) : 0;
  if (!Number.isNaN(expected) && expected > 0 && identities.length !== expected) {
    throw new Error(
      `Grafana secrets transport: setup identity count ${identities.length} does not match LOAD_TEST_EXPECTED_IDENTITY_COUNT (${expected}).`,
    );
  }

  const withTokens = identities.filter((row) => row?.bearerToken);
  if (withTokens.length < 1) {
    throw new Error('Grafana secrets transport: setup identities contain no bearer tokens.');
  }

  setRuntimeIdentityPool(withTokens);
  grafanaSecretsRuntimeApplied = true;
}

/** Test-only: reset per-VU apply guard between selfcheck cases. */
export function resetGrafanaSecretsRuntimeApplyState() {
  grafanaSecretsRuntimeApplied = false;
}

export function buildGrafanaSecretsSetupResult(identities, env = __ENV) {
  const secretPartCount = parseInt(env.LOAD_TEST_IDENTITIES_SECRET_COUNT, 10);
  if (Number.isNaN(secretPartCount) || secretPartCount < 1) {
    throw new Error(`Invalid LOAD_TEST_IDENTITIES_SECRET_COUNT for setup proof: ${env.LOAD_TEST_IDENTITIES_SECRET_COUNT}`);
  }

  const expectedRaw = env.LOAD_TEST_EXPECTED_IDENTITY_COUNT;
  const expected = expectedRaw ? parseInt(expectedRaw, 10) : 0;
  if (!Number.isNaN(expected) && expected > 0 && identities.length !== expected) {
    throw new Error(`Identity count mismatch: expected ${expected}, reconstructed ${identities.length}.`);
  }

  if (identities.length < 1) {
    throw new Error('Grafana secrets transport reconstructed an empty identity pool.');
  }

  return {
    identityTransport: 'grafana-secrets',
    identityCount: identities.length,
    secretPartCount,
    identities,
  };
}

export function formatGrafanaSecretsRuntimeProof(setupResult) {
  return (
    `LOAD_TEST_IDENTITY_PROOF transport=${setupResult.identityTransport} ` +
    `identityCount=${setupResult.identityCount} secretPartCount=${setupResult.secretPartCount}`
  );
}
