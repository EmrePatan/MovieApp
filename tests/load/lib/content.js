import { SharedArray } from 'k6/data';
import { loadContentPools, loadSearchTerms } from './config.js';
import { identityPool } from './identities.js';

const pools = new SharedArray('content-pools', () => [loadContentPools()]);
const pool = () => pools[0];
const searchTerms = new SharedArray('search-terms', () => [loadSearchTerms()]);
const terms = () => searchTerms[0];

function resolveVuId(vu) {
  if (typeof vu === 'number' && vu > 0) {
    return vu;
  }
  return __VU;
}

export function identityForVu(vu) {
  if (identityPool.length === 0) {
    return null;
  }
  const vuId = resolveVuId(vu);
  return identityPool[(vuId - 1) % identityPool.length];
}

export function pickMovieId(seed) {
  const data = pool();
  if (!data.movieIds || data.movieIds.length === 0) {
    throw new Error('No movie IDs configured in content pool');
  }
  const idx = Math.abs(seed) % data.movieIds.length;
  return data.movieIds[idx];
}

export function pickTvShowId(seed) {
  const data = pool();
  if (!data.tvShowIds || data.tvShowIds.length === 0) {
    throw new Error('No TV show IDs configured in content pool');
  }
  const idx = Math.abs(seed) % data.tvShowIds.length;
  return data.tvShowIds[idx];
}

export function pickSearchTerm(seed) {
  const list = terms();
  if (!list || list.length === 0) {
    return 'star';
  }
  return list[Math.abs(seed) % list.length];
}

export function pickWarmExternalMovieId(seed) {
  const data = pool();
  if (!data.externalRatingsWarmMovieIds || data.externalRatingsWarmMovieIds.length === 0) {
    return null;
  }
  const idx = Math.abs(seed) % data.externalRatingsWarmMovieIds.length;
  return data.externalRatingsWarmMovieIds[idx];
}

export function pickWarmExternalTvId(seed) {
  const data = pool();
  if (!data.externalRatingsWarmTvShowIds || data.externalRatingsWarmTvShowIds.length === 0) {
    return null;
  }
  const idx = Math.abs(seed) % data.externalRatingsWarmTvShowIds.length;
  return data.externalRatingsWarmTvShowIds[idx];
}

export function contentPoolStats() {
  const data = pool();
  const list = terms();
  return {
    movieCount: data.movieIds?.length || 0,
    tvShowCount: data.tvShowIds?.length || 0,
    warmExternalMovieCount: data.externalRatingsWarmMovieIds?.length || 0,
    warmExternalTvCount: data.externalRatingsWarmTvShowIds?.length || 0,
    identityCount: identityPool.length,
    searchTermCount: list?.length || 0,
  };
}
