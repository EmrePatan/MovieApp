import { parseJsonOpen } from './jsonText.js';

export function loadThresholds() {
  const path = __ENV.LOAD_TEST_THRESHOLDS_FILE
    ? __ENV.LOAD_TEST_THRESHOLDS_FILE.replace(/\\/g, '/')
    : '../config/thresholds.json';
  const parsed = parseJsonOpen(path);
  const thresholds = { ...parsed.global };

  for (const [group, rules] of Object.entries(parsed.groups || {})) {
    for (const [metric, expr] of Object.entries(rules)) {
      thresholds[`${metric}{group:${group}}`] = expr;
    }
  }

  for (const [requestName, rules] of Object.entries(parsed.names || {})) {
    const observeOnly = rules.abortOnFail === false;
    for (const [metric, expr] of Object.entries(rules)) {
      if (metric === 'abortOnFail') {
        continue;
      }
      const key = `${metric}{name:${requestName}}`;
      const expressions = Array.isArray(expr) ? expr : [expr];
      thresholds[key] = observeOnly
        ? expressions.map((threshold) => ({ threshold, abortOnFail: false }))
        : expressions;
    }
  }

  Object.assign(thresholds, parsed.applicationScoped || {});

  return thresholds;
}
