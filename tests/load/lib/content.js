import { loadContentPools, loadSearchTerms, identityPool } from './config.js';

const pools = loadContentPools();
const searchTerms = loadSearchTerms();

export function identityForVu(vu) {
  if (identityPool.length === 0) {
    return null;
  }
  return identityPool[(vu - 1) % identityPool.length];
}

export function pickMovieId(seed) {
  if (pools.movieIds.length === 0) {
    throw new Error('No movie IDs configured in content pool');
  }
  const idx = Math.abs(seed) % pools.movieIds.length;
  return pools.movieIds[idx];
}

export function pickTvShowId(seed) {
  if (pools.tvShowIds.length === 0) {
    throw new Error('No TV show IDs configured in content pool');
  }
  const idx = Math.abs(seed) % pools.tvShowIds.length;
  return pools.tvShowIds[idx];
}

export function pickSearchTerm(seed) {
  if (searchTerms.length === 0) {
    return 'star';
  }
  return searchTerms[Math.abs(seed) % searchTerms.length];
}

export function pickWarmExternalMovieId(seed) {
  if (pools.externalRatingsWarmMovieIds.length === 0) {
    return null;
  }
  const idx = Math.abs(seed) % pools.externalRatingsWarmMovieIds.length;
  return pools.externalRatingsWarmMovieIds[idx];
}

export function pickWarmExternalTvId(seed) {
  if (pools.externalRatingsWarmTvShowIds.length === 0) {
    return null;
  }
  const idx = Math.abs(seed) % pools.externalRatingsWarmTvShowIds.length;
  return pools.externalRatingsWarmTvShowIds[idx];
}

export function contentPoolStats() {
  return {
    movieCount: pools.movieIds.length,
    tvShowCount: pools.tvShowIds.length,
    warmExternalMovieCount: pools.externalRatingsWarmMovieIds.length,
    warmExternalTvCount: pools.externalRatingsWarmTvShowIds.length,
    identityCount: identityPool.length,
    searchTermCount: searchTerms.length,
  };
}
