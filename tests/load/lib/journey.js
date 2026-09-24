import { apiGet } from './http.js';
import {
  pickMovieId,
  pickTvShowId,
  pickSearchTerm,
  pickWarmExternalMovieId,
  pickWarmExternalTvId,
} from './content.js';
import { includeExternalRatings } from './config.js';
import {
  thinkScrollFeed,
  thinkDetailPage,
  thinkShortGlance,
  thinkSearchTyping,
  thinkLibraryBrowse,
  thinkInsights,
} from './thinktime.js';

/**
 * Weighted session model — see tests/load/README.md for rationale.
 * Each iteration selects ONE primary session, then optional micro-actions.
 */
const SESSION_WEIGHTS = [
  { key: 'home_feed', weight: 32 },
  { key: 'detail_open', weight: 28 },
  { key: 'discover_browse', weight: 14 },
  { key: 'search', weight: 6 },
  { key: 'library', weight: 6 },
  { key: 'insights', weight: 3 },
  { key: 'reviews_surface', weight: 5 },
  { key: 'home_split', weight: 6 },
];

function pickWeighted(weights) {
  const total = weights.reduce((sum, w) => sum + w.weight, 0);
  let roll = Math.random() * total;
  for (const entry of weights) {
    roll -= entry.weight;
    if (roll <= 0) {
      return entry.key;
    }
  }
  return weights[weights.length - 1].key;
}

export function runUserJourney({ vu, iter, token }) {
  if (!token) {
    throw new Error('User-concurrency scenario requires at least one bearer token identity');
  }

  const seed = vu * 1000 + iter;
  const session = pickWeighted(SESSION_WEIGHTS);

  switch (session) {
    case 'home_feed':
      runHomeFeed(token);
      break;
    case 'home_split':
      runHomeSplit(token);
      break;
    case 'detail_open':
      runDetailOpen(seed, token);
      break;
    case 'discover_browse':
      runDiscover(seed);
      break;
    case 'search':
      runSearch(seed);
      break;
    case 'library':
      runLibrary(token);
      break;
    case 'insights':
      runInsights(token);
      break;
    case 'reviews_surface':
      runReviewsSurface(seed);
      break;
    default:
      runHomeFeed(token);
  }
}

function runHomeFeed(token) {
  if (Math.random() < 0.7) {
    apiGet('/api/home?type=all&sectionSize=10', { group: 'home', token });
  } else {
    apiGet('/api/home/personalized?type=all&sectionSize=10', { group: 'personalized', token });
  }

  if (Math.random() < 0.25) {
    apiGet('/api/recommendations/home', { group: 'personalized', token, name: 'recommendations-home' });
  }

  thinkScrollFeed();
}

function runHomeSplit(token) {
  apiGet('/api/home/browse?type=all&sectionSize=10', { group: 'home', token, name: 'home-browse' });
  thinkShortGlance();
  apiGet('/api/home/personalized?type=all&sectionSize=10', { group: 'personalized', token });
  thinkScrollFeed();
}

function runDetailOpen(seed, token) {
  const isMovie = Math.random() < 0.52;
  if (isMovie) {
    const movieId = pickMovieId(seed);
    apiGet(`/api/movies/${movieId}`, { group: 'movie-detail' });
    thinkShortGlance();
    runMovieDetailStatus(movieId, token, seed);
    if (includeExternalRatings()) {
      const warmId = pickWarmExternalMovieId(seed) || movieId;
      apiGet(`/api/movies/${warmId}/external-ratings`, { group: 'external-ratings-warm' });
    }
  } else {
    const tvId = pickTvShowId(seed + 1);
    apiGet(`/api/tvshows/${tvId}`, { group: 'tv-detail' });
    thinkShortGlance();
    runTvDetailStatus(tvId, token, seed);
    if (includeExternalRatings()) {
      const warmId = pickWarmExternalTvId(seed) || tvId;
      apiGet(`/api/tvshows/${warmId}/external-ratings`, { group: 'external-ratings-warm' });
    }
  }

  thinkDetailPage();
}

function runMovieDetailStatus(movieId, token, seed) {
  apiGet(`/api/favorites/movies/${movieId}/status`, { group: 'detail-status', token });
  apiGet(`/api/watchlists/membership?mediaType=movie&contentId=${movieId}`, {
    group: 'detail-status',
    token,
    name: 'watchlist-membership-movie',
  });
  apiGet(`/api/ratings/movies/${movieId}/me`, { group: 'ratings-me', token });
  apiGet(`/api/watch-history/movies/${movieId}/me`, { group: 'detail-status', token });
  apiGet(`/api/movies/${movieId}/follow`, { group: 'detail-status', token, name: 'movie-follow' });

  if (Math.random() < 0.35) {
    apiGet(`/api/movies/${movieId}/credits`, { group: 'movie-detail', name: 'movie-credits' });
  }
  if (Math.random() < 0.2) {
    apiGet(`/api/ratings/movies/${movieId}`, { group: 'ratings-me', name: 'ratings-summary-movie' });
  }
  if (Math.random() < 0.3) {
    apiGet(`/api/reviews/movies/${movieId}?page=1&pageSize=20`, { group: 'reviews' });
  }
}

function runTvDetailStatus(tvId, token, seed) {
  apiGet(`/api/favorites/tvshows/${tvId}/status`, { group: 'detail-status', token });
  apiGet(`/api/watchlists/membership?mediaType=tv&contentId=${tvId}`, {
    group: 'detail-status',
    token,
    name: 'watchlist-membership-tv',
  });
  apiGet(`/api/ratings/tvshows/${tvId}/me`, { group: 'ratings-me', token });
  apiGet(`/api/tvshows/${tvId}/follow`, { group: 'detail-status', token, name: 'tv-follow' });

  if (Math.random() < 0.25) {
    apiGet(`/api/tvshows/${tvId}/seasons/1`, { group: 'tv-detail', name: 'tv-season-1' });
  }
  if (Math.random() < 0.3) {
    apiGet(`/api/reviews/tvshows/${tvId}?page=1&pageSize=20`, { group: 'reviews' });
  }
}

function runDiscover(seed) {
  const mode = seed % 3;
  if (mode === 0) {
    apiGet('/api/discovery/trending?type=all&page=1&pageSize=20', { group: 'discover' });
  } else if (mode === 1) {
    apiGet('/api/discovery/popular?type=all&page=1&pageSize=20', { group: 'discover' });
  } else {
    apiGet('/api/discovery/explore-preview?sectionSize=10', { group: 'discover', name: 'explore-preview' });
  }
  thinkScrollFeed();
}

function runSearch(seed) {
  const term = pickSearchTerm(seed);
  thinkSearchTyping();
  if (Math.random() < 0.65) {
    apiGet(`/api/search/autocomplete?q=${encodeURIComponent(term)}`, { group: 'search', name: 'autocomplete' });
  } else {
    // Unified search is IP rate-limited (Search:RateLimit). Keep low volume in user-concurrency runs.
    apiGet(`/api/search?q=${encodeURIComponent(term)}&type=all&page=1&pageSize=20`, { group: 'search', name: 'unified-search' });
  }
}

function runLibrary(token) {
  const categories = ['watching', 'watched', 'liked', 'watchlist'];
  const category = categories[Math.floor(Math.random() * categories.length)];
  apiGet(`/api/library?category=${category}&mediaType=all&page=1&pageSize=20`, { group: 'library', token });
  thinkLibraryBrowse();
}

function runInsights(token) {
  apiGet('/api/insights/v3?timeZone=UTC', { group: 'insights', token });
  if (Math.random() < 0.3) {
    apiGet('/api/insights/summary?timeZone=UTC', { group: 'insights', token, name: 'insights-summary' });
  }
  thinkInsights();
}

function runReviewsSurface(seed) {
  const movieId = pickMovieId(seed);
  apiGet(`/api/reviews/movies/${movieId}?page=1&pageSize=20`, { group: 'reviews' });
  thinkShortGlance();
}
