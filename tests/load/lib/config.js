import { SharedArray } from 'k6/data';

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

export const contentDataset = () => (__ENV.LOAD_TEST_CONTENT_DATASET || 'hot').toLowerCase();

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

export const dataPath = (fileName) => {
  const root = __ENV.LOAD_TEST_DATA_DIR || `${__ENV.PWD || '.'}/tests/load/data`;
  return `${root}/${fileName}`;
};

function readJsonFile(path) {
  const raw = open(path);
  return JSON.parse(raw);
}

export function loadContentPools() {
  const dataset = contentDataset();
  const file =
    dataset === 'varied' ? 'varied-content.json' : 'hot-content.json';
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

export const identityPool = new SharedArray('identities', function loadIdentities() {
  const file = __ENV.LOAD_TEST_TOKENS_FILE;
  if (file) {
    const parsed = JSON.parse(open(file));
    return (parsed.identities || []).filter((i) => i.bearerToken && i.bearerToken !== 'REPLACE_WITH_JWT');
  }

  const inline = [];
  for (let i = 1; i <= 200; i += 1) {
    const token = __ENV[`LOAD_TEST_TOKEN_${String(i).padStart(3, '0')}`];
    if (token) {
      inline.push({ id: `env-token-${i}`, bearerToken: token });
    }
  }
  if (inline.length > 0) {
    return inline;
  }

  try {
    const parsed = JSON.parse(open(dataPath('tokens.json')));
    return (parsed.identities || []).filter((i) => i.bearerToken && i.bearerToken !== 'REPLACE_WITH_JWT');
  } catch (_) {
    return [];
  }
});

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
