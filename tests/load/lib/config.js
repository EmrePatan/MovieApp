import { parseJsonOpen } from './jsonText.js';

const required = (name) => {
  const value = __ENV[name];
  if (!value || value.trim() === '') {
    throw new Error(`Missing required environment variable: ${name}`);
  }
  return value.trim();
};

export const baseUrl = () => {
  const raw = required('LOAD_TEST_BASE_URL');
  return raw.endsWith('/') ? raw.slice(0, -1) : raw;
};

export const scenarioName = () => __ENV.LOAD_TEST_SCENARIO || 'user-concurrency';

export const SUPPORTED_CONTENT_DATASETS = ['hot', 'varied'];

export function assertSupportedContentDataset(value) {
  const raw = value === undefined || value === null || String(value).trim() === '' ? 'hot' : String(value);
  const dataset = raw.toLowerCase().trim();
  if (!SUPPORTED_CONTENT_DATASETS.includes(dataset)) {
    throw new Error(
      `Unsupported LOAD_TEST_CONTENT_DATASET: "${raw}". Supported values: ${SUPPORTED_CONTENT_DATASETS.join(', ')}.`,
    );
  }
  return dataset;
}

export const contentDataset = () => assertSupportedContentDataset(__ENV.LOAD_TEST_CONTENT_DATASET);

export const presetName = () => (__ENV.LOAD_TEST_PRESET || 'smoke').toLowerCase();

export const stageTarget = () => {
  const raw = __ENV.LOAD_TEST_STAGE_TARGET;
  if (!raw) {
    return null;
  }
  const n = parseInt(raw, 10);
  if (Number.isNaN(n) || n < 1) {
    throw new Error(`Invalid LOAD_TEST_STAGE_TARGET: ${raw}`);
  }
  return n;
};

export const backendCommitSha = () => __ENV.LOAD_TEST_BACKEND_SHA || 'unknown';

export const loadTestCommitSha = () => __ENV.LOAD_TEST_TOOL_SHA || 'unknown';

export const environmentLabel = () => __ENV.LOAD_TEST_ENVIRONMENT || 'unspecified';

export const acceptLanguage = () => __ENV.LOAD_TEST_ACCEPT_LANGUAGE || 'en-US';

export const requestTimeout = () => {
  const ms = parseInt(__ENV.LOAD_TEST_HTTP_TIMEOUT_MS || '30000', 10);
  return `${ms}ms`;
};

export const includeExternalRatings = () =>
  (__ENV.LOAD_TEST_INCLUDE_EXTERNAL_RATINGS || 'false').toLowerCase() === 'true';

/**
 * Search in main user-concurrency capacity runs:
 * - off: no search traffic (default for stage >= 250 VUs)
 * - autocomplete-only: no unified/movie/tv search (not IP-rate-limited)
 * - realistic: autocomplete + rare unified search (single-IP distortion risk)
 *
 * Override with LOAD_TEST_SEARCH_PROFILE. High-VU capacity measurement must use off or autocomplete-only.
 */
export function searchProfile() {
  const explicit = (__ENV.LOAD_TEST_SEARCH_PROFILE || '').toLowerCase();
  if (explicit === 'off' || explicit === 'autocomplete-only' || explicit === 'realistic') {
    return explicit;
  }

  const stage = stageTarget();
  if (stage !== null && stage >= 250) {
    return 'off';
  }
  return 'autocomplete-only';
}

function resolveDataDirRoot() {
  const raw = __ENV.LOAD_TEST_DATA_DIR;
  if (!raw || raw.trim() === '') {
    return null;
  }
  const root = raw.replace(/\\/g, '/').replace(/\/$/, '');
  const isGrafanaCloud = (__ENV.LOAD_TEST_EXECUTION_MODE || '').toLowerCase() === 'grafana-cloud';
  if (isGrafanaCloud && /^[A-Za-z]:\//.test(root)) {
    return null;
  }
  return root;
}

export const dataPath = (fileName) => {
  const root = resolveDataDirRoot();
  if (root) {
    return `${root}/${fileName}`;
  }
  // Relative to tests/load/lib/ when scenarios import lib modules.
  return `../data/${fileName}`;
};

function readJsonFile(path) {
  return parseJsonOpen(path);
}

function resolveContentFileName(dataset) {
  const base = dataset === 'varied' ? 'varied-content.json' : 'hot-content.json';
  const production = base.replace('.json', '.production.json');
  try {
    open(dataPath(production));
    return production;
  } catch (_) {
    return base;
  }
}

export function loadContentPools() {
  const dataset = contentDataset();
  const file = resolveContentFileName(dataset);
  const fallback = dataset === 'varied' ? 'varied-content.example.json' : 'hot-content.example.json';
  let parsed;
  try {
    parsed = readJsonFile(dataPath(file));
  } catch (_) {
    parsed = readJsonFile(dataPath(fallback));
  }
  return {
    movieIds: parsed.movieIds || [],
    tvShowIds: parsed.tvShowIds || [],
    externalRatingsWarmMovieIds: parsed.externalRatingsWarmMovieIds || [],
    externalRatingsWarmTvShowIds: parsed.externalRatingsWarmTvShowIds || [],
  };
}

export function loadSearchTerms() {
  try {
    return readJsonFile(dataPath('search-terms.json')).terms || [];
  } catch (_) {
    return readJsonFile(dataPath('search-terms.example.json')).terms || [];
  }
}

export function buildStagesFromPreset(presets) {
  const preset = presets[presetName()] || presets.smoke;
  const target = stageTarget() || parseInt(__ENV.LOAD_TEST_VUS || '5', 10);
  const use1000 = target === 1000 && presets.capacityStage1000;
  const timing = use1000 ? presets.capacityStage1000 : preset;
  return [
    { duration: timing.rampUp, target },
    { duration: timing.hold, target },
    { duration: timing.rampDown, target: 0 },
  ];
}
