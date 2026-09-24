/**
 * Grafana Cloud k6 execution extras (load zone, run name).
 * Applied only when LOAD_TEST_EXECUTION_MODE=grafana-cloud so local `k6 run` is unchanged.
 */

const DEFAULT_EU_LOAD_ZONES = [
  'amazon:de:frankfurt',
  'amazon:ie:dublin',
  'amazon:gb:london',
  'amazon:fr:paris',
  'amazon:se:stockholm',
  'amazon:it:milan',
];

export function cloudLoadZone() {
  const raw = (__ENV.LOAD_TEST_CLOUD_LOAD_ZONE || 'amazon:de:frankfurt').trim();
  return raw;
}

export function isGrafanaCloudExecution() {
  return (__ENV.LOAD_TEST_EXECUTION_MODE || '').toLowerCase() === 'grafana-cloud';
}

export function applyCloudOptions(options) {
  if (!isGrafanaCloudExecution()) {
    return options;
  }

  const zone = cloudLoadZone();
  if (!zone) {
    return options;
  }

  const runName =
    __ENV.LOAD_TEST_CLOUD_RUN_NAME ||
    `movie-cave-${__ENV.LOAD_TEST_SCENARIO || 'run'}-${__ENV.LOAD_TEST_STAGE_TARGET || 'custom'}`;

  options.cloud = {
    name: runName,
    distribution: {
      primary: { loadZone: zone, percent: 100 },
    },
  };

  return options;
}

export { DEFAULT_EU_LOAD_ZONES };
