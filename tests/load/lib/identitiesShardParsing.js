export function normalizeIdentityList(parsed) {
  const source = Array.isArray(parsed) ? parsed : parsed.identities || [];
  const list = source.filter((i) => i?.bearerToken && i.bearerToken !== 'REPLACE_WITH_JWT');
  return list.length > 0 ? list : null;
}

export function parseIdentitiesPayload(raw) {
  const parsed = JSON.parse(raw);
  const list = normalizeIdentityList(parsed);
  if (!list) {
    throw new Error('Identity payload parsed but no valid bearer tokens were found.');
  }
  return list;
}

export function assembleShardedIdentitiesPayload(shardParts) {
  if (!Array.isArray(shardParts) || shardParts.length < 1) {
    throw new Error('At least one identity shard part is required.');
  }
  return parseIdentitiesPayload(shardParts.join(''));
}

export function loadIdentitiesFromShardEnv(env, shardCount) {
  if (!shardCount || shardCount < 1) {
    throw new Error(`Invalid LOAD_TEST_IDENTITIES_SHARD_COUNT: ${shardCount}`);
  }

  const parts = [];
  for (let i = 1; i <= shardCount; i += 1) {
    const key = `LOAD_TEST_IDENTITIES_JSON_${String(i).padStart(3, '0')}`;
    const part = env[key];
    if (!part || part.trim() === '') {
      throw new Error(`Missing identity shard environment variable: ${key}`);
    }
    parts.push(part);
  }

  return assembleShardedIdentitiesPayload(parts);
}
