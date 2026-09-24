import { identityPool } from './identities.js';

let runtimeIdentityPool = null;

export function setRuntimeIdentityPool(identities) {
  runtimeIdentityPool = identities;
}

export function getEffectiveIdentityPool() {
  if (Array.isArray(runtimeIdentityPool) && runtimeIdentityPool.length > 0) {
    return runtimeIdentityPool;
  }

  return identityPool;
}

export function getRuntimeIdentityTransportLabel() {
  if (Array.isArray(runtimeIdentityPool) && runtimeIdentityPool.length > 0) {
    return 'grafana-secrets';
  }

  return (__ENV.LOAD_TEST_IDENTITIES_TRANSPORT || 'env').toLowerCase();
}
