export function loadThresholds() {
  const path = __ENV.LOAD_TEST_THRESHOLDS_FILE
    ? __ENV.LOAD_TEST_THRESHOLDS_FILE.replace(/\\/g, '/')
    : '../config/thresholds.json';
  const parsed = JSON.parse(open(path));
  const thresholds = { ...parsed.global };

  for (const [group, rules] of Object.entries(parsed.groups || {})) {
    for (const [metric, expr] of Object.entries(rules)) {
      thresholds[`${metric}{group:${group}}`] = expr;
    }
  }

  Object.assign(thresholds, parsed.applicationScoped || {});

  return thresholds;
}
