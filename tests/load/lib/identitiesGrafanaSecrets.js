import secrets from 'k6/secrets';
import { assembleShardedIdentitiesPayload } from './identitiesShardParsing.js';

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
